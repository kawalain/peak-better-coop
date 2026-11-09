using System.Collections.Generic;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class NumberingMachineTests
{
    [Fact]
    public void The_wheels_start_on_the_old_figures_and_rest_on_the_new()
    {
        Assert.Equal("No. 0000000", NumberingMachine.Shown("No. 0000000", "No. 6772859", 0f));
        float end = NumberingMachine.Seconds("No. 0000000", "No. 6772859");
        Assert.Equal("No. 6772859", NumberingMachine.Shown("No. 0000000", "No. 6772859", end));
        Assert.Equal("No. 6772859", NumberingMachine.Shown("No. 0000000", "No. 6772859", end + 5f));
    }

    [Fact]
    public void Each_wheel_only_turns_forward_a_figure_at_a_time()
    {
        string before = "0000";
        for (int step = 1; step <= 200; step++)
        {
            string now = NumberingMachine.Shown("0000", "9415", step * 0.005f);
            for (int i = 0; i < now.Length; i++)
            {
                int turned = ((now[i] - before[i]) % 10 + 10) % 10;
                Assert.True(turned <= 1, $"wheel {i} jumped from {before[i]} to {now[i]}");
            }

            before = now;
        }

        Assert.Equal("9415", before);
    }

    [Fact]
    public void The_wheels_come_to_rest_from_left_to_right()
    {
        var restedAt = new List<float>();
        for (int wheel = 0; wheel < 4; wheel++)
        {
            for (float t = 0f; t < 2f; t += 0.01f)
            {
                if (NumberingMachine.Shown("1111", "5555", t)[wheel] == '5')
                {
                    restedAt.Add(t);
                    break;
                }
            }
        }

        for (int i = 1; i < restedAt.Count; i++)
        {
            Assert.True(restedAt[i] > restedAt[i - 1]);
        }
    }

    [Fact]
    public void Only_the_wheels_whose_figure_changes_turn()
    {
        Assert.Equal("10:41 AM", NumberingMachine.Shown("10:41 AM", "10:42 AM", 0f));
        Assert.Equal(NumberingMachine.StepSeconds, NumberingMachine.Seconds("10:41 AM", "10:42 AM"), 3);
        // Two wheels turn: the second rests a gap after the first.
        string turning = NumberingMachine.Shown("10:49 AM", "10:50 AM", 0.03f);
        Assert.StartsWith("10:", turning);
        Assert.EndsWith(" AM", turning);
        Assert.Equal(NumberingMachine.StepSeconds + NumberingMachine.GapSeconds, NumberingMachine.Seconds("10:49 AM", "10:50 AM"), 3);
    }

    [Fact]
    public void A_full_turn_spins_even_the_wheels_already_set()
    {
        Assert.Equal(0f, NumberingMachine.Seconds("123", "123"));
        Assert.True(NumberingMachine.Seconds("123", "123", fullTurn: true) > 0f);
        Assert.NotEqual("123", NumberingMachine.Shown("123", "123", 0.05f, fullTurn: true));
    }

    [Fact]
    public void Text_of_another_layout_shows_at_once()
    {
        Assert.Equal("NOV 9 2026", NumberingMachine.Shown("2026. 11. 9.", "NOV 9 2026", 0f));
        Assert.Equal(0f, NumberingMachine.Seconds("2026. 11. 9.", "NOV 9 2026"));
    }

    [Fact]
    public void Noughts_keep_the_type_and_clear_the_figures()
    {
        Assert.Equal("No. 0000000", NumberingMachine.Noughts("No. 6772859"));
    }
}
