using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BetterCoop.Core;
using BetterCoop.Features;
using BetterCoop.Net;
using Newtonsoft.Json.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zorro.Core.CLI;
using Zorro.UI;

namespace BetterCoop.DevTools;

/// <summary>
/// Everything the Control Line can do: a fixed list, each command named and
/// explained, reading or calling what a player could reach in the game.
/// </summary>
internal static partial class ControlCommands
{
    private const int MaxLogLines = 2000;

    private static readonly Dictionary<int, Selectable> Listed = new();

    private static Dictionary<string, MethodInfo>? consoleCommands;

    /// <summary>
    /// Console commands left out: they change what the Steam account or this
    /// Windows user keeps (achievements, unlocked ascents and cosmetics, the
    /// saved look and run settings), which outlasts any test.
    /// </summary>
    private static readonly HashSet<string> KeepsOut = new(StringComparer.OrdinalIgnoreCase)
    {
        "AchievementManager", "Ascents", "PassportManager", "CharacterCustomization",
        "RunSettings.Increment", "RunSettings.ResetAll",
        // Ends the Run for the whole room and counts toward the account's achievements; drill end does it in a Twin.
        "Character.TestWin",
    };

    public static readonly Dictionary<string, ControlCommand> All = new(StringComparer.Ordinal)
    {
        ["commands"] = new("Every command with what it takes.", _ => Commands()),
        ["hello"] = new("Who this copy is: role, process, version, scene.", _ => Hello()),
        ["state"] = new("The scene, the room and its players, this copy's scout, and the mod's state.", _ => State()),
        ["log"] = new("{lines?, contains?}: the last lines of this copy's BepInEx log.", Log),
        ["screenshot"] = new("{scale?}: a picture of the screen, rendered at 1 to 4 times its size, saved as a PNG; answers with its path.", Screenshot),
        ["controls"] = new("{contains?}: the controls that can be pressed now, with ids for 'press'.", Controls),
        ["press"] = new("{id, right?}: presses a control listed by 'controls', as a left (or right) click.", Press),
        ["console"] = new("{name?, args?}: runs one of the game's own console commands, but none that change what the account keeps; without a name, lists them.", Console),
        ["warp"] = new("{x, y, z} or {to: actor}: moves this copy's scout there, as the game's own warp.", Warp),
        ["menu"] = new("{action: pause|back}: opens the pause menu, or goes back as the pause key does.", Menu),
        ["scroll"] = new("{to}: scrolls the open paper form to the first text containing 'to'.", Scroll),
        ["depart"] = new("At the Airport: boards for the Run as the boarding pass does, through the mod's own checks.", _ => Depart()),
        ["order"] = new("{what, ...}: a Roster order, under the Roster's rules; without what, lists them.", Order),
        ["perf"] = new("{seconds?}: for that long (default 5), times every Update of the mod's own components and every patch it adds to the game; answers with the mod's milliseconds and bytes per frame and the costliest methods.", ControlPerf.Measure),
        ["photo"] = new("{actor}: takes that player's passport photo again, here and now, and saves it as a PNG; answers with its path.", Photo),
        ["drill"] = new("{what, ...}: forge {key, actor?, value?} writes a made-up value to a mod key of the room, or of player 'actor', as a player who may not write it would (Pairs, without a value, swaps every Pair's roles), and the other copies should drop it; letter {case} plays the Zoo's Pair letter case (0 to 5); end {ending: summit|lost}, in a Twin only, ends the real Run for the whole room.", Drill),
    };

    private static JToken Commands() =>
        new JObject(All.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => new JProperty(c.Key, c.Value.Help)));

    private static JToken Hello() => new JObject
    {
        ["role"] = Twin.Role,
        ["unmodded"] = Twin.Unmodded,
        ["pid"] = System.Diagnostics.Process.GetCurrentProcess().Id,
        ["version"] = MyPluginInfo.PLUGIN_VERSION,
        ["scene"] = SceneManager.GetActiveScene().name,
        ["inRoom"] = PhotonNetwork.InRoom,
    };

    private static JToken State()
    {
        var state = new JObject
        {
            ["scene"] = SceneManager.GetActiveScene().name,
            ["connection"] = PhotonNetwork.NetworkClientState.ToString(),
            ["room"] = Room(),
            ["me"] = Scout(Character.localCharacter),
            ["scouts"] = new JArray(Character.AllCharacters.Where(c => c != null).Select(Scout)),
        };
        AddModState(state);
        return state;
    }

    /// <summary>Adds the mod's own state: Pairs, Run Rules, the Run Report.</summary>
    static partial void AddModState(JObject state);

    private static JToken Room()
    {
        if (!PhotonNetwork.InRoom)
        {
            return JValue.CreateNull();
        }

        return new JObject
        {
            ["name"] = PhotonNetwork.CurrentRoom.Name,
            ["region"] = PhotonNetwork.CloudRegion,
            ["masterActor"] = PhotonNetwork.MasterClient?.ActorNumber,
            ["myActor"] = PhotonNetwork.LocalPlayer.ActorNumber,
            ["players"] = new JArray(PhotonNetwork.PlayerList.Select(p => new JObject
            {
                ["actor"] = p.ActorNumber,
                ["name"] = KnownPlayers.NameOf(p, p.ActorNumber),
                ["scoutId"] = KnownPlayers.IdOf(p),
                ["master"] = p.IsMasterClient,
                ["me"] = p.IsLocal,
                ["inactive"] = p.IsInactive,
                ["modVersion"] = ModPresence.VersionOf(p),
                ["role"] = ModPresence.RoleOf(p).ToString(),
            })),
        };
    }

    private static JToken Scout(Character? character)
    {
        if (character == null)
        {
            return JValue.CreateNull();
        }

        Vector3 at = character.Center;
        return new JObject
        {
            ["actor"] = character.photonView?.Owner?.ActorNumber,
            ["name"] = character.characterName,
            ["me"] = character.IsLocal,
            ["position"] = new JArray(Math.Round(at.x, 2), Math.Round(at.y, 2), Math.Round(at.z, 2)),
            ["dead"] = character.data.dead,
            ["passedOut"] = character.data.passedOut,
            ["carried"] = character.data.isCarried,
            ["carrying"] = character.data.carriedPlayer != null ? character.data.carriedPlayer.photonView?.Owner?.ActorNumber : null,
        };
    }

    private static JToken Log(JObject args)
    {
        int lines = Math.Clamp((int?)args["lines"] ?? 80, 1, MaxLogLines);
        string contains = (string?)args["contains"] ?? string.Empty;
        string file = Path.Combine(Paths.BepInExRootPath, "LogOutput.log");
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var tail = new Queue<string>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (contains.Length > 0 && line.IndexOf(contains, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            tail.Enqueue(line);
            if (tail.Count > lines)
            {
                tail.Dequeue();
            }
        }

        return string.Join("\n", tail);
    }

    private static IEnumerator Screenshot(JObject args, Action<JToken> reply)
    {
        int scale = Math.Clamp((int?)args["scale"] ?? 1, 1, 4);
        yield return new WaitForEndOfFrame();
        Texture2D picture = ScreenCapture.CaptureScreenshotAsTexture(scale);
        try
        {
            string file = Path.Combine(Paths.CachePath, "BetterCoop-control-screen.png");
            File.WriteAllBytes(file, picture.EncodeToPNG());
            reply(file);
        }
        finally
        {
            UnityEngine.Object.Destroy(picture);
        }
    }

    private static JToken Controls(JObject args)
    {
        string contains = (string?)args["contains"] ?? string.Empty;
        Listed.Clear();
        var found = new JArray();
        foreach (Selectable control in UnityEngine.Object.FindObjectsByType<Selectable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!control.isActiveAndEnabled || !control.IsInteractable() || control is not IPointerClickHandler)
            {
                continue;
            }

            string text = string.Join(" / ", control.GetComponentsInChildren<TMP_Text>()
                .Where(t => t.isActiveAndEnabled && !string.IsNullOrWhiteSpace(t.text))
                .Select(t => t.text.Trim())
                .Take(3));
            string path = PathOf(control.transform);
            if (contains.Length > 0
                && text.IndexOf(contains, StringComparison.OrdinalIgnoreCase) < 0
                && path.IndexOf(contains, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            int id = control.GetInstanceID();
            Listed[id] = control;
            found.Add(new JObject
            {
                ["id"] = id,
                ["kind"] = control.GetType().Name,
                ["text"] = text,
                ["path"] = path,
            });
        }

        return found;
    }

    private static string PathOf(Transform transform)
    {
        var names = new List<string>();
        for (Transform? at = transform; at != null && names.Count < 6; at = at.parent)
        {
            names.Add(at.name);
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private static JToken Press(JObject args)
    {
        int id = (int?)args["id"] ?? throw new ArgumentException("needs id, from 'controls'");
        if (!Listed.TryGetValue(id, out Selectable control) || control == null)
        {
            throw new ArgumentException($"no control {id}; list them again with 'controls'");
        }

        if (!control.isActiveAndEnabled || !control.IsInteractable())
        {
            throw new InvalidOperationException($"{control.name} cannot be pressed now");
        }

        var click = new PointerEventData(EventSystem.current)
        {
            button = (bool?)args["right"] == true ? PointerEventData.InputButton.Right : PointerEventData.InputButton.Left,
        };
        ExecuteEvents.Execute(control.gameObject, click, ExecuteEvents.pointerClickHandler);
        return $"pressed {PathOf(control.transform)}";
    }

    private static JToken Console(JObject args)
    {
        consoleCommands ??= FindConsoleCommands();
        string? name = (string?)args["name"];
        if (string.IsNullOrEmpty(name))
        {
            return new JArray(consoleCommands.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c =>
                $"{c.Key}({string.Join(", ", c.Value.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})"));
        }

        if (!consoleCommands.TryGetValue(name!, out MethodInfo method))
        {
            throw new ArgumentException($"no console command {name}; list them with no name");
        }

        ParameterInfo[] parameters = method.GetParameters();
        JArray given = args["args"] as JArray ?? new JArray();
        if (given.Count != parameters.Length)
        {
            throw new ArgumentException($"{name} takes {parameters.Length} argument(s)");
        }

        object?[] values = parameters.Select((p, i) => Convert(given[i], p.ParameterType)).ToArray();
        object? result = method.Invoke(null, values);
        return result == null ? $"ran {name}" : JToken.FromObject(result.ToString());
    }

    private static object? Convert(JToken value, Type type)
    {
        if (type.IsEnum)
        {
            return Enum.Parse(type, value.ToString(), ignoreCase: true);
        }

        if (type == typeof(Vector3) && value is JArray xyz && xyz.Count == 3)
        {
            return new Vector3((float)xyz[0], (float)xyz[1], (float)xyz[2]);
        }

        return System.Convert.ChangeType(value.ToString(), type, CultureInfo.InvariantCulture);
    }

    /// <summary>The game's static methods marked as console commands, by "Type.Method".</summary>
    private static Dictionary<string, MethodInfo> FindConsoleCommands()
    {
        var found = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (Type type in AccessToolsTypes(typeof(Character).Assembly))
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (method.GetCustomAttribute<ConsoleCommandAttribute>() != null
                    && !KeepsOut.Contains(type.Name)
                    && !KeepsOut.Contains($"{type.Name}.{method.Name}"))
                {
                    found[$"{type.Name}.{method.Name}"] = method;
                }
            }
        }

        return found;
    }

    private static IEnumerable<Type> AccessToolsTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException partial)
        {
            return partial.Types.Where(t => t != null)!;
        }
    }

    private static JToken Menu(JObject args)
    {
        switch ((string?)args["action"])
        {
            case "pause":
                GUIManager.instance.pauseMenu.SetActive(true);
                return "paused";
            case "back":
                return Back();
            default:
                throw new ArgumentException("action: pause or back");
        }
    }

    /// <summary>
    /// Goes back as the pause key does in the pause menu: a paper form takes
    /// it first, a page goes to its parent, the menu closes. The key itself
    /// cannot be pressed from here, as the game reads it afresh every frame.
    /// </summary>
    private static JToken Back()
    {
        GameObject menu = GUIManager.instance.pauseMenu;
        PauseMenuHandler? handler = menu != null && menu.activeInHierarchy ? menu.GetComponent<PauseMenuHandler>() : null;
        if (handler == null)
        {
            throw new InvalidOperationException("the pause menu is not open");
        }

        if (handler.currentPage is BetterCoop.Ui.Paper.PaperPage paper && paper.TakeBack())
        {
            return "the form took it";
        }

        if (handler.currentPage is IHaveParentPage child)
        {
            (UIPage page, PageTransistion transition) = child.GetParentPage();
            handler.TransistionToPage(page, transition);
            return $"back to {page.name}";
        }

        menu!.SetActive(false);
        return "closed the pause menu";
    }

    private static JToken Scroll(JObject args)
    {
        string to = (string?)args["to"] ?? throw new ArgumentException("needs to");
        foreach (BetterCoop.Ui.Paper.PaperPage page in UnityEngine.Object.FindObjectsByType<BetterCoop.Ui.Paper.PaperPage>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            TMP_Text? found = page.GetComponentsInChildren<TMP_Text>()
                .FirstOrDefault(t => t.isActiveAndEnabled && t.text != null && t.text.IndexOf(to, StringComparison.OrdinalIgnoreCase) >= 0);
            if (found != null)
            {
                page.Reveal(found.rectTransform);
                return $"scrolled to {found.text}";
            }
        }

        throw new ArgumentException($"no open paper shows '{to}'");
    }

    private static JToken Photo(JObject args)
    {
        int actor = (int?)args["actor"] ?? throw new ArgumentException("actor: whose photo");
        BetterCoop.Ui.Paper.LivePortraits.ForgetAll();
        if (new BetterCoop.Ui.Paper.LivePortraits().Photo(actor) is not RenderTexture photo)
        {
            throw new InvalidOperationException($"no photo of {actor} can be taken now");
        }

        RenderTexture? active = RenderTexture.active;
        var picture = new Texture2D(photo.width, photo.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = photo;
            picture.ReadPixels(new Rect(0, 0, photo.width, photo.height), 0, 0);
            picture.Apply(false);
            string path = Path.Combine(Paths.CachePath, $"BetterCoop.photo-{actor}.png");
            File.WriteAllBytes(path, picture.EncodeToPNG());
            return path;
        }
        finally
        {
            RenderTexture.active = active;
            UnityEngine.Object.Destroy(picture);
        }
    }

    private static JToken Drill(JObject args)
    {
        string what = (string?)args["what"] ?? throw new ArgumentException("what: forge, letter, end");
        switch (what)
        {
            case "forge":
                return Forge(args);
            case "letter":
                int scene = (int?)args["case"] ?? 0;
                if (scene < 0 || scene >= ZooLetter.Scenes.Length)
                {
                    throw new ArgumentException($"case: 0 to {ZooLetter.Scenes.Length - 1}");
                }

                ZooLetter.Play(ZooLetter.Scenes[scene], seed: scene + 1);
                return ZooLetter.Scenes[scene].Label();
            case "end":
                return EndRun((string?)args["ending"] ?? "summit");
            default:
                throw new ArgumentException($"no drill '{what}'; drills: forge, letter, end");
        }
    }

    /// <summary>Ends the real Run for everyone, as the game's own win check or a party lost would; only in a Twin, which leaves the account alone.</summary>
    private static JToken EndRun(string ending)
    {
        if (!Twin.Active)
        {
            throw new InvalidOperationException("ending a real Run is only for Twins, which leave the account alone");
        }

        if (!GameHandler.IsOnIsland || Character.localCharacter == null)
        {
            throw new InvalidOperationException("no Run under way here");
        }

        switch (ending)
        {
            case "summit":
                Character.localCharacter.photonView.RPC("RPCEndGame_ForceWin", Photon.Pun.RpcTarget.All);
                return "the Run ends at the summit";
            case "lost":
                if (!PhotonNetwork.IsMasterClient)
                {
                    throw new InvalidOperationException("only the Troop Leader's copy loses the whole party");
                }

                foreach (Character scout in Character.AllCharacters.Where(c => c != null && !c.data.dead))
                {
                    scout.photonView.RPC("RPCA_Die", Photon.Pun.RpcTarget.All);
                }

                return "the party is lost";
            default:
                throw new ArgumentException("ending: summit or lost");
        }
    }

    private static JToken Forge(JObject args)
    {

        if (!PhotonNetwork.InRoom)
        {
            throw new InvalidOperationException("not in a room");
        }

        string key = (string?)args["key"] ?? throw new ArgumentException("key: a mod key, such as Pairs or SwapRequest");
        if (!key.StartsWith("BetterCoop.", StringComparison.Ordinal))
        {
            key = "BetterCoop." + key;
        }

        object? value = args["value"] switch
        {
            JArray list => list.Select(item => (int)item).ToArray(),
            JValue { Type: JTokenType.Integer } number => (int)number,
            JValue { Type: JTokenType.String } text => (string)text!,
            _ when key == RoomKeys.Pairs && PairRoster.Read() is PairingResult current => PairRoster.Encode(new PairingResult(
                current.Pairs.Select(p => new Pair(p.Rider, p.Carrier)).ToList(), current.Unpaired, current.PreferenceOverridden)),
            _ => "forged",
        };
        var forged = new ExitGames.Client.Photon.Hashtable { [key] = value };
        if (args["actor"] is JToken actor)
        {
            Photon.Realtime.Player player = PhotonNetwork.CurrentRoom.GetPlayer((int)actor) ?? throw new ArgumentException($"no player {actor}");
            player.SetCustomProperties(forged);
        }
        else
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(forged);
        }

        return $"wrote {key}";
    }

    private static JToken Depart()
    {
        AirportCheckInKiosk kiosk = UnityEngine.Object.FindAnyObjectByType<AirportCheckInKiosk>()
            ?? throw new InvalidOperationException("no check-in kiosk here; depart from the Airport");
        BoardingPass pass = GUIManager.instance.boardingPass;
        pass.kiosk = kiosk;
        pass.StartGame();
        return "boarding";
    }

    private static JToken Warp(JObject args)
    {
        Character me = Character.localCharacter ?? throw new InvalidOperationException("no scout of this copy to warp");
        Vector3 to;
        if (args["to"] is JToken actor)
        {
            Character other = Character.AllCharacters.FirstOrDefault(c => c != null && c.photonView?.Owner?.ActorNumber == (int)actor)
                ?? throw new ArgumentException($"no scout of actor {actor}");
            to = other.Center + Vector3.up * 1.5f;
        }
        else
        {
            to = new Vector3((float?)args["x"] ?? throw new ArgumentException("needs x, y, z or to"), (float)args["y"]!, (float)args["z"]!);
        }

        me.photonView.RPC(nameof(Character.WarpPlayerRPC), RpcTarget.All, to, true);
        return new JArray(Math.Round(to.x, 2), Math.Round(to.y, 2), Math.Round(to.z, 2));
    }
}
