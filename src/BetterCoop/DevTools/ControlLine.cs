using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BetterCoop.DevTools;

/// <summary>
/// The Control Line: lets a tool on this computer watch and steer this copy
/// of the game, for testing without a second person. It listens on
/// 127.0.0.1 only, on a port of the system's choosing, and answers only
/// requests that carry the token it makes at every start. It announces
/// itself with a file under the Windows user's home (see
/// <see cref="Registry"/>), which <c>tools/peak_mcp.py</c> reads. It is off
/// unless asked for: the launch argument <see cref="Argument"/>, the
/// environment variable <c>BETTERCOOP_CONTROL=1</c>, or <c>[Debug] Control Line</c>.
/// </summary>
/// <remarks>
/// One request is one line of JSON, <c>{"token", "command", "args"}</c>, and
/// its answer one line, <c>{"ok", "result"}</c> or <c>{"ok": false, "error"}</c>.
/// Requests run on the game's main thread, a few a frame, through the fixed
/// set of commands in <see cref="ControlCommands"/>: nothing runs code it was
/// sent, and nothing feeds raw keys or mouse moves.
/// </remarks>
internal sealed class ControlLine : MonoBehaviour
{
    public const string Argument = "--bettercoop-control";

    private const int MaxRequest = 64 * 1024;

    private const int PerFrame = 8;

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private static ConfigEntry<bool>? wanted;

    private readonly ConcurrentQueue<Job> jobs = new();

    private TcpListener? listener;

    private Thread? accepting;

    private string token = string.Empty;

    private string? registration;

    private volatile bool closing;

    /// <summary>The folder every copy that listens announces itself in.</summary>
    public static string Registry =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bettercoop", "control");

    public static void Bind(ConfigFile config) =>
        wanted = config.Bind("Debug", "Control Line", false,
            "Development builds only: let tools/peak_mcp.py watch and steer this copy of the game over 127.0.0.1. Read when the game starts.");

    public static bool Asked =>
        Environment.GetCommandLineArgs().Contains(Argument)
        || Environment.GetEnvironmentVariable("BETTERCOOP_CONTROL") == "1"
        || wanted?.Value == true;

    private void Start()
    {
        try
        {
            token = NewToken();
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Announce(port);
            accepting = new Thread(Accept) { IsBackground = true, Name = "BetterCoop Control Line" };
            accepting.Start();
            Plugin.Log.LogWarning($"Control Line: listening on 127.0.0.1:{port} for tools on this computer.");
        }
        catch (Exception exception)
        {
            Guard.Report("ControlLine.Start", exception);
            enabled = false;
        }
    }

    private void Update()
    {
        for (int i = 0; i < PerFrame && jobs.TryDequeue(out Job job); i++)
        {
            Run(job);
        }
    }

    private void OnDestroy() => Close();

    private void OnApplicationQuit() => Close();

    private void Close()
    {
        closing = true;
        try
        {
            listener?.Stop();
        }
        catch (Exception)
        {
            // Stopping a listener that already failed is of no interest.
        }

        try
        {
            if (registration != null && File.Exists(registration))
            {
                File.Delete(registration);
            }
        }
        catch (Exception exception)
        {
            Guard.Report("ControlLine.Close", exception);
        }
    }

    private static string NewToken()
    {
        byte[] bytes = new byte[24];
        using (var random = RandomNumberGenerator.Create())
        {
            random.GetBytes(bytes);
        }

        return string.Concat(bytes.Select(b => b.ToString("x2")));
    }

    private void Announce(int port)
    {
        Directory.CreateDirectory(Registry);
        int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
        foreach (string stale in Directory.GetFiles(Registry, "*.json"))
        {
            // A copy that crashed leaves its file; the tool skips those it cannot reach.
            if (File.GetLastWriteTimeUtc(stale) < DateTime.UtcNow.AddDays(-1))
            {
                File.Delete(stale);
            }
        }

        registration = Path.Combine(Registry, $"{pid}.json");
        var card = new JObject
        {
            ["pid"] = pid,
            ["port"] = port,
            ["token"] = token,
            ["role"] = Twin.Role,
            ["profile"] = Paths.BepInExRootPath,
            ["log"] = Path.Combine(Paths.BepInExRootPath, "LogOutput.log"),
        };
        File.WriteAllText(registration, card.ToString(Formatting.Indented));
    }

    private void Accept()
    {
        while (!closing)
        {
            TcpClient client;
            try
            {
                client = listener!.AcceptTcpClient();
            }
            catch (Exception)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(_ => Serve(client));
        }
    }

    private void Serve(TcpClient client)
    {
        using (client)
        {
            try
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;
                NetworkStream stream = client.GetStream();
                JObject answer = Answer(ReadLine(stream));
                byte[] bytes = Encoding.UTF8.GetBytes(answer.ToString(Formatting.None) + "\n");
                stream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception)
            {
                // A tool that hung up or sent nonsense gets nothing more.
            }
        }
    }

    private static string ReadLine(NetworkStream stream)
    {
        var bytes = new List<byte>();
        int b;
        while ((b = stream.ReadByte()) >= 0 && b != '\n')
        {
            if (bytes.Count >= MaxRequest)
            {
                throw new InvalidDataException("request too long");
            }

            bytes.Add((byte)b);
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private JObject Answer(string line)
    {
        JObject request;
        try
        {
            request = JObject.Parse(line);
        }
        catch (Exception)
        {
            return Fail("not JSON");
        }

        if (!SameToken((string?)request["token"]))
        {
            return Fail("wrong token");
        }

        var job = new Job((string?)request["command"] ?? string.Empty, request["args"] as JObject ?? new JObject());
        jobs.Enqueue(job);
        return job.Done.Wait(Patience) ? job.Answer! : Fail("the game did not answer in time");
    }

    private bool SameToken(string? given)
    {
        if (given == null || given.Length != token.Length)
        {
            return false;
        }

        int difference = 0;
        for (int i = 0; i < token.Length; i++)
        {
            difference |= given[i] ^ token[i];
        }

        return difference == 0;
    }

    private void Run(Job job)
    {
        try
        {
            if (!ControlCommands.All.TryGetValue(job.Command, out ControlCommand command))
            {
                job.Finish(Fail($"no command {job.Command}; try 'commands'"));
            }
            else if (command.Later != null)
            {
                StartCoroutine(RunLater(job, command.Later));
            }
            else
            {
                job.Finish(Succeed(command.Now!(job.Args)));
            }
        }
        catch (Exception exception)
        {
            job.Finish(Fail(exception.Message));
        }
    }

    private static IEnumerator RunLater(Job job, Func<JObject, Action<JToken>, IEnumerator> later)
    {
        IEnumerator steps;
        try
        {
            steps = later(job.Args, result => job.Finish(Succeed(result)));
        }
        catch (Exception exception)
        {
            job.Finish(Fail(exception.Message));
            yield break;
        }

        while (true)
        {
            try
            {
                if (!steps.MoveNext())
                {
                    break;
                }
            }
            catch (Exception exception)
            {
                job.Finish(Fail(exception.Message));
                yield break;
            }

            yield return steps.Current;
        }

        job.Finish(Fail("the command ended without an answer"));
    }

    private static JObject Succeed(JToken? result) => new() { ["ok"] = true, ["result"] = result ?? JValue.CreateNull() };

    private static JObject Fail(string error) => new() { ["ok"] = false, ["error"] = error };

    private sealed class Job
    {
        public Job(string command, JObject args)
        {
            Command = command;
            Args = args;
        }

        public string Command { get; }

        public JObject Args { get; }

        public ManualResetEventSlim Done { get; } = new();

        public JObject? Answer { get; private set; }

        /// <summary>Answers once; later answers are dropped.</summary>
        public void Finish(JObject answer)
        {
            if (Answer == null)
            {
                Answer = answer;
                Done.Set();
            }
        }
    }
}

/// <summary>A command on the Control Line: answered at once, or over frames.</summary>
internal sealed class ControlCommand
{
    public ControlCommand(string help, Func<JObject, JToken?> now)
    {
        Help = help;
        Now = now;
    }

    public ControlCommand(string help, Func<JObject, Action<JToken>, IEnumerator> later)
    {
        Help = help;
        Later = later;
    }

    public string Help { get; }

    public Func<JObject, JToken?>? Now { get; }

    public Func<JObject, Action<JToken>, IEnumerator>? Later { get; }
}
