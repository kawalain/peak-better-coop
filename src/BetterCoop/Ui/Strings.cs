namespace BetterCoop.Ui;

/// <summary>
/// Player-facing text in Korean and English, following the game language.
/// Korean matches the game's own Korean: formal endings (합니다체) and the
/// Korean terms in GLOSSARY.md.
/// </summary>
internal static class Strings
{
    internal static bool Korean => LocalizedText.CURRENT_LANGUAGE == LocalizedText.Language.Korean;

    public static string ModDisabled => Korean
        ? "코옵: 지금 게임 버전과 맞지 않아 모드를 껐습니다."
        : "Coop: the mod is off because it does not match this game version.";

    public static string ModDisabledByCoopMod => Korean
        ? "코옵: Co-op Mod와 함께 쓸 수 없어 모드를 껐습니다. Co-op Mod를 지워 주세요."
        : "Coop: the mod is off because Co-op Mod is also installed. Remove Co-op Mod to play.";

    public static string PairFormed(string carrier, string rider) => Korean
        ? $"코옵: {carrier} 님이 {rider} 님을 업습니다. {carrier} 님은 키보드, {rider} 님은 마우스로 조작합니다."
        : $"Coop: {carrier} (Carrier, keyboard) carries {rider} (Rider, mouse).";

    public static string PreferenceOverridden(string player) => Korean
        ? $"코옵: 원하는 역할이 겹쳐 {player} 님은 다른 역할을 맡습니다."
        : $"Coop: roles clashed, so {player} takes the other role.";

    public static string Unpaired(string player) => Korean
        ? $"코옵: {player} 님은 지금은 짝 없이 올라갑니다."
        : $"Coop: {player} plays without a Pair for now.";

    public static string PairDissolved(string carrier, string rider) => Korean
        ? $"코옵: {carrier} 님과 {rider} 님의 짝이 풀렸습니다."
        : $"Coop: the Pair of {carrier} and {rider} has ended.";

    public static string ShuffleCountdown(int seconds) => Korean
        ? $"코옵: {seconds}초 뒤 짝을 섞습니다."
        : $"Coop: shuffling Pairs in {seconds}...";

    public static string RoleSwapCountdown(int seconds) => Korean
        ? $"코옵: {seconds}초 뒤 자리를 바꿉니다."
        : $"Coop: swapping places in {seconds}...";

    public static string LatePairingCountdown(int seconds) => Korean
        ? $"코옵: {seconds}초 뒤 짝을 이어 줍니다."
        : $"Coop: pairing you up in {seconds}...";

    public static string Roster => Korean ? "편성표" : "Roster";

    public static string MenuRolePreference => Korean ? "원하는 역할" : "Role Preference";

    public static string RoleName(Core.RolePreference preference) => preference switch
    {
        Core.RolePreference.Carrier => Korean ? "업는 사람" : "Carrier",
        Core.RolePreference.Rider => Korean ? "업힌 사람" : "Rider",
        _ => Korean ? "상관없음" : "Either",
    };

    /// <summary>The Roster's printed head: what form it is.</summary>
    public static string FormTitle => Korean ? "BetterCoop 서식 제1호" : "BetterCoop Form No. 1";

    public static string FormMembers(int count) => Korean ? $"대원 {count}명" : count == 1 ? "1 scout" : $"{count} scouts";

    public static string MenuYou => Korean ? "나의 짝" : "Your Pair";

    public static string MenuPartner => Korean ? "짝" : "Partner";

    public static string MenuYourRole => Korean ? "나의 역할" : "Your role";

    public static string RoleOf(bool carrying) => carrying
        ? Korean ? "업는 사람" : "Carrier"
        : Korean ? "업힌 사람" : "Rider";

    public static string MenuNoPartner => Korean ? "지금은 짝이 없습니다" : "You have no partner for now";

    public static string MenuNextShuffle => Korean ? "다음 짝 섞기" : "Next shuffle";

    public static string MenuShuffleNow => Korean ? "진행 중" : "Now";

    public static string MenuShuffleOff => Korean ? "꺼짐" : "Off";

    public static string Clock(double seconds)
    {
        int whole = (int)System.Math.Ceiling(seconds);
        return $"{whole / 60}:{whole % 60:00}";
    }

    public static string MenuPairsForm => Korean ? "짝은 섬에 도착하면 편성됩니다" : "Pairs form on the island";

    public static string MenuPairs => Korean ? "짝 명단" : "Pairs";

    public static string MenuPairPlan => Korean ? "짝 계획 (출발 후)" : "Pair Plan (after departure)";

    public static string MenuPlanHelp => Korean
        ? "출발 전에 짝을 미리 정해 둘 수 있습니다. 섬에 도착하면 이 계획대로 짝을 편성하고, 계획에 없는 대원은 제비뽑기로 짝을 짓습니다."
        : "Set the Pairs before departure. On arrival the Pairs form as planned; anyone left out is paired by lot.";

    public static string MenuPlanNone => Korean
        ? "분대장이 아직 짝 계획을 세우지 않았습니다. 섬에 도착하면 제비뽑기로 짝을 짓습니다."
        : "The Troop Leader has not planned the Pairs yet; on arrival they are drawn by lot.";

    public static string AboutPlan => Korean
        ? "통보하면 모두에게 알리고, 섬에 도착할 때 이대로 짝을 편성합니다."
        : "Announced, everyone is told, and the Pairs form this way on arrival.";

    public static string PlanAnnounced => Korean
        ? "코옵: 분대장이 짝 계획을 통보했습니다. ESC → 편성표에서 확인할 수 있습니다."
        : "Coop: the Troop Leader has planned the Pairs. See ESC → Roster.";

    public static string MenuPairNumber(int number) => Korean ? $"{number}조" : $"Pair {number}";

    public static string MenuTroopLeader => Korean ? "분대장 지시" : "Troop Leader's orders";

    public static string MenuTroopLeaderSign => Korean ? "분대장 확인" : "Troop Leader";

    public static string MenuShuffleNowLabel => Korean ? "지금 짝 섞기" : "Shuffle Pairs now";

    public static string MenuShufflePlanLabel => Korean ? "제비뽑기로 짝 계획" : "Draw a plan by lot";

    public static string AboutShufflePlan => Korean
        ? "짝 계획 초안을 제비뽑기로 채웁니다. 통보해야 계획이 됩니다."
        : "Fills the draft with Pairs drawn by lot; announce it to make it the plan.";

    public static string MenuRecover => Korean ? "모든 짝 다시 업히기" : "Re-carry every Pair";

    public static string StampShuffle => Korean ? "섞기" : "Shuffle";

    public static string StampRestore => Korean ? "복구" : "Restore";

    public static string StampReally => Korean ? "정말로?" : "Really?";

    public static string Serial(int number) => $"No. {number:D7}";

    /// <summary>A notice's words without the "Coop:" the game's log needs to tell them from its own.</summary>
    public static string Unprefixed(string message)
    {
        foreach (string prefix in new[] { "코옵: ", "Coop: " })
        {
            if (message.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                return message.Substring(prefix.Length);
            }
        }

        return message;
    }

    public static string NoticeHeading(Core.NoticeKind kind) => kind switch
    {
        Core.NoticeKind.Memo => Korean ? "메모" : "Memo",
        Core.NoticeKind.Tag => Korean ? "꼬리표" : "Baggage tag",
        Core.NoticeKind.Caution => Korean ? "주의 통지" : "Notice",
        _ => Korean ? "전보" : "Telegram",
    };

    /// <summary>The word on the rubber stamp of a caution.</summary>
    public static string CautionStamp => Korean ? "주의" : "Caution";

    /// <summary>The Pair letter's heading: what is changing.</summary>
    public static string LetterHeading(Core.OperationKind kind) => kind switch
    {
        Core.OperationKind.RoleSwap => Korean ? "역할 교대 통지" : "Role swap orders",
        Core.OperationKind.LatePairing => Korean ? "짝 배정 통지" : "Pairing orders",
        Core.OperationKind.PairAssignment => Korean ? "분대장 편성 통지" : "Troop Leader's orders",
        Core.OperationKind.None => Korean ? "짝 편성 통지" : "Pair orders",
        _ => Korean ? "짝 섞기 통지" : "Shuffle orders",
    };

    /// <summary>Under the photos while the countdown runs.</summary>
    public static string LetterSoon(int seconds) => Korean ? $"{seconds}초 뒤 몸이 바뀝니다" : $"Changing places in {seconds}";

    public static string LetterDone => Korean ? "새 짝과 함께 오릅니다" : "Climbing with your partner";

    public static string LetterAlone => Korean ? "이번에는 짝 없이 오릅니다" : "Climbing alone for now";

    /// <summary>The postmark's word once the change is made.</summary>
    public static string LetterArrived => Korean ? "도착" : "Arrived";

    /// <summary>The partner not yet known, as before a Pair Shuffle.</summary>
    public static string LetterUnknown => Korean ? "누구일까요?" : "Who?";

    public static string LetterYou => Korean ? "나" : "You";

    public static string LetterTo(string player) => Korean ? $"받는 이: {player} 대원" : $"To: Scout {player}";

    public static string LetterRoleUnknown => Korean ? "역할 미정" : "Role to come";

    /// <summary>The heading printed on a slip that asks before going on.</summary>
    public static string SlipQuery => Korean ? "확인 요청" : "Query";

    public static string StampNotify => Korean ? "통보" : "Notified";

    /// <summary>The post office round a postmark's top.</summary>
    public static string PostmarkOffice => Korean ? "섬 분대 우체국" : "ISLAND TROOP P.O.";

    /// <summary>The date round a postmark's foot.</summary>
    public static string PostmarkDate(System.DateTime day) => Korean
        ? day.ToString("yyyy. M. d.", System.Globalization.CultureInfo.InvariantCulture)
        : day.ToString("MMM d yyyy", System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant();

    public static string StampDiscard => Korean ? "버리기" : "Discard";

    public static string StampKeep => Korean ? "계속 쓰기" : "Keep";

    public static string DiscardDraft => Korean ? "초안을 버리겠습니까?" : "Throw the draft away?";

    public static string DiscardDraftNote => Korean
        ? "통보하지 않은 짝 명단 초안이 있습니다.\n버리면 지금의 짝 명단으로 돌아갑니다."
        : "The Pairs you laid out have not been announced.\nThrown away, the list goes back to the Pairs as they are.";

    public static string MenuDraft => Korean
        ? "초안입니다. 통보하기 전에는 아무도 바뀌지 않습니다."
        : "This is a draft. Nobody changes until you announce it.";

    public static string StampAsk => Korean ? "요청" : "Request";

    public static string StampCancel => Korean ? "취소" : "Cancel";

    public static string MenuBusy => Korean
        ? "코옵: 다른 짝 변경이 진행 중이라 지금은 할 수 없습니다."
        : "Coop: another Pair change is under way; try again in a moment.";

    public static string MenuRunRules => Korean ? "원정 규칙" : "Run Rules";

    public static string Minutes(int minutes) => Korean ? $"{minutes}분" : $"{minutes} min";

    public static string RuleCampfireShuffle => Korean ? "모닥불에서 짝 섞기" : "Campfire Shuffle";

    public static string RuleTimedShuffle => Korean ? "시간마다 짝 섞기" : "Timed Shuffle";

    public static string RuleTimedShuffleInterval => Korean ? "짝 섞기 간격" : "Timed Shuffle Interval";

    public static string RuleHandOverPlace => Korean ? "몸 인계: 자리" : "Hand Over Place";

    public static string RuleHandOverItems => Korean ? "몸 인계: 아이템" : "Hand Over Items";

    public static string RuleCarryWeight => Korean ? "업은 사람 무게" : "Carry Weight";

    public static string RuleRiderPicksFromBackpack => Korean ? "가방은 업힌 사람이 고르기" : "Rider Picks From Backpack";

    public static string RuleAllowOddPlayerCount => Korean ? "홀수 인원 출발 허용" : "Allow Odd Player Count";

    public static string RuleFollowRolePreferences => Korean ? "대원 희망 역할 들어주기" : "Honour role wishes";

    public static string RuleAllowSwapRequests => Korean ? "대원끼리 자리 바꾸기" : "Scouts may swap places";

    public static string RuleLatePairing => Korean ? "짝 없는 대원 이어주기" : "Pair up the partnerless";

    public static string RulesPairs => Korean ? "짝" : "Pairs";

    public static string RulesShuffles => Korean ? "짝 섞기" : "Shuffles";

    public static string RulesHandover => Korean ? "몸 인계" : "Body Handover";

    public static string RulesCarrying => Korean ? "업기" : "Carrying";

    public static string AboutFollowRolePreferences => Korean
        ? "짝을 정할 때 대원이 원하는 역할을 되도록 들어줍니다. 끄면 역할을 제비뽑기로 정합니다."
        : "Scouts get the role they wish for where it fits; off, roles are drawn by lot.";

    public static string AboutAllowSwapRequests => Korean
        ? "짝 둘이 함께 요청하면 업는 사람과 업힌 사람을 바꿀 수 있습니다."
        : "The two of a Pair may trade Carrier and Rider when both ask.";

    public static string AboutLatePairing => Korean
        ? "짝 없는 대원이 둘 생기면 바로 짝을 지어 줍니다. 끄면 다음 짝 섞기까지 혼자 오릅니다."
        : "Two scouts without a partner are paired at once; off, they climb alone until the next shuffle.";

    public static string AboutRolePreferenceIgnored => Korean
        ? "분대장이 역할을 제비뽑기로 정하기로 했습니다."
        : "The Troop Leader draws roles by lot for now.";

    public static string AboutSwapNotAllowed => Korean
        ? "분대장이 자리 바꾸기를 막아 두었습니다."
        : "The Troop Leader has not allowed swapping places.";

    public static string SwapNotAllowed => Korean
        ? "코옵: 분대장이 자리 바꾸기를 막아 두었습니다."
        : "Coop: the Troop Leader has not allowed swapping places.";

    public static string AboutRolePreference => Korean
        ? "짝 편성과 짝 섞기에서 원하는 역할입니다. 다음 번부터 적용됩니다."
        : "The role you would like when Pairs are formed or shuffled; applies from the next time.";

    public static string AboutSwapPlaces => Korean
        ? "둘 다 요청하면 업는 사람과 업힌 사람이 바뀝니다."
        : "When both of you ask, you trade Carrier and Rider.";

    public static string AboutNoPartner => Korean
        ? "다른 짝 없는 사람과 짝 이어주기가 되거나, 짝 섞기 때 짝이 생깁니다."
        : "You get a partner when another player is free or the Pairs shuffle.";

    public static string AboutNextShuffle => Korean
        ? "시간마다 짝 섞기까지 남은 시간입니다."
        : "Time left until the next Timed Shuffle.";

    public static string AboutShuffleNow => Korean
        ? "모든 짝을 지금 새로 정합니다."
        : "Forms every Pair anew now.";

    public static string AboutRecover => Korean
        ? "업기가 풀렸을 때 모든 짝을 다시 업히고 업힌 사람을 깨웁니다."
        : "Re-attaches every Pair's carry and wakes every Rider.";

    public static string AboutAssign => Korean
        ? "통보하면 모두에게 알린 뒤 3초 뒤에 옮긴 그대로 짝이 됩니다. 짝 없는 사람은 짝 이어주기로 묶입니다."
        : "Announced, everyone is told and the Pairs become as you laid them out three seconds later; anyone left Unpaired pairs up by Late Pairing.";

    public static string MenuBoardHelp => Korean
        ? "사진을 끌어 옮기거나, 눌러서 든 다음 놓을 곳을 누릅니다. 사진 위에 놓으면 서로 자리를 바꿉니다. 오른쪽 클릭이나 ESC로 제자리에 둡니다."
        : "Drag a photo, or press it to pick it up and press where it goes. Dropped on a photo, the two trade places. A right click or Esc puts it back.";

    public static string MenuNewPair => Korean ? "새 짝" : "New";

    public static string MenuDropHere => Korean ? "여기에 놓기" : "Drop here";

    public static string MenuEmptyPlace => Korean ? "빈자리" : "Empty";

    public static string MenuAssignReset => Korean ? "처음 편성으로" : "Start over";

    public static string AboutCampfireShuffle => Korean
        ? "모닥불을 피워 다음 구간으로 갈 때 짝을 새로 정합니다."
        : "Forms the Pairs anew when a campfire is lit.";

    public static string AboutTimedShuffle => Korean
        ? "정해진 간격마다 짝을 새로 정합니다."
        : "Forms the Pairs anew at a fixed interval.";

    public static string AboutTimedShuffleInterval => Korean
        ? "시간마다 짝 섞기의 간격입니다."
        : "How often a Timed Shuffle comes.";

    public static string AboutHandOverPlace => Korean
        ? "짝이 바뀌면 새 업는 사람이 넘겨받는 몸의 자리로 갑니다."
        : "A new Carrier moves to where the body they take over stood.";

    public static string AboutHandOverItems => Korean
        ? "짝이 바뀌면 새 업는 사람이 넘겨받는 몸의 아이템을 가집니다."
        : "A new Carrier takes the items of the body they take over.";

    public static string AboutCarryWeight => Korean
        ? "업는 사람이 업힌 사람의 무게를 느낍니다."
        : "Carriers feel the weight of the person they carry.";

    public static string AboutRiderPicksFromBackpack => Korean
        ? "가방 휠에서 업힌 사람이 아이템을 고릅니다. 끄면 업는 사람이 고릅니다."
        : "The Rider picks from the backpack wheel; off, the Carrier does.";

    public static string AboutAllowOddPlayerCount => Korean
        ? "인원이 홀수여도 출발합니다. 남는 사람은 짝 없이 올라갑니다."
        : "Lets a Run depart with an odd count; one player climbs without a Pair.";

    public static string MenuUnpaired => Korean ? "짝 없는 사람" : "Unpaired";

    public static string AssignmentCountdown(int seconds) => Korean
        ? $"코옵: {seconds}초 뒤 분대장이 정한 짝이 됩니다."
        : $"Coop: the Troop Leader's Pairs take effect in {seconds}...";

    public static string MenuAssign => Korean ? "짝 명단 통보" : "Announce the Pairs";

    public static string MenuAssignUnavailable => Korean
        ? "코옵: 지금은 이대로 짝을 지을 수 없습니다. 새로 업는 사람은 깨어 있어야 하고, 쓰러진 짝은 함께 있어야 하며, 모두 같은 버전의 모드가 있어야 합니다."
        : "Coop: these Pairs cannot be set now. A new Carrier must be awake, a fallen Pair stays together, and everyone needs this version.";

    public static string SwapPlaces => Korean ? "자리 바꾸기" : "Swap Places";

    public static string CancelSwap => Korean ? "자리 바꾸기 취소" : "Cancel Swap";

    public static string SwapUnavailable => Korean
        ? "코옵: 짝과 둘 다 섬에서 살아 있을 때만 자리를 바꿀 수 있습니다."
        : "Coop: you can swap places only while you and your partner are alive on the island.";

    public static string SwapAsked(string partner) => Korean
        ? $"코옵: {partner} 님에게 자리 바꾸기를 요청했습니다. 30초 안에 상대도 누르면 바꿉니다."
        : $"Coop: asked {partner} to swap places. It happens if they agree within 30 seconds.";

    public static string SwapOffered(string partner) => Korean
        ? $"코옵: {partner} 님이 자리를 바꾸고 싶어 합니다. ESC → 편성표에서 자리 바꾸기를 누르면 바꿉니다."
        : $"Coop: {partner} wants to swap places. Press Swap Places in ESC → Roster to agree.";

    public static string SwapWithdrawn(string partner) => Korean
        ? $"코옵: {partner} 님의 자리 바꾸기 요청이 취소되었습니다."
        : $"Coop: {partner} no longer asks to swap places.";

    public static string SwapCancelled => Korean
        ? "코옵: 자리 바꾸기 요청을 취소했습니다."
        : "Coop: your request to swap places is cancelled.";

    public static string SwapExpired => Korean
        ? "코옵: 30초 동안 답이 없어 자리 바꾸기 요청이 취소되었습니다."
        : "Coop: no answer in 30 seconds, so your request to swap places lapsed.";

    public static string SwapCancelledByShuffle => Korean
        ? "코옵: 짝 섞기가 시작되어 자리 바꾸기 요청을 취소했습니다."
        : "Coop: a Pair Shuffle is starting, so your request to swap places is cancelled.";

    public static string RiderHandEmptied => Korean
        ? "코옵: 업힌 사람은 아이템을 손에 들 수 없어 바닥에 내려놓았습니다."
        : "Coop: a Rider cannot hold items, so it was dropped.";

    public static string RiderBackEmptied => Korean
        ? "코옵: 업힌 사람은 등에 아이템을 멜 수 없어 바닥에 내려놓았습니다."
        : "Coop: a Rider cannot wear a back item, so it was dropped.";

    public static string Recovered => Korean
        ? "코옵: 분대장이 모든 짝을 다시 업히고 업힌 사람을 깨웠습니다."
        : "Coop: the Troop Leader re-carried every Pair and woke every Rider.";

    public static string SoloPairOn(int latencyMs) => Korean
        ? $"코옵: 1인 짝 테스트를 켰습니다. (마우스 지연 {latencyMs}ms)"
        : $"Coop: Solo Pair is on (mouse delayed {latencyMs} ms).";

    public static string SoloPairOff => Korean
        ? "코옵: 1인 짝 테스트를 껐습니다."
        : "Coop: Solo Pair is off.";

    public static string OddPlayerCount(int count) => Korean
        ? $"코옵: 출발하려면 인원이 짝수여야 합니다. (현재 {count}명)"
        : $"Coop: an even number of players is needed to depart. ({count} in the room)";

    public static string MissingMod(string player) => Korean
        ? $"코옵: {player} 님에게 모드가 없습니다."
        : $"Coop: {player} does not have the mod.";

    public static string OtherVersion(string player, string theirs, string required) => Korean
        ? $"코옵: {player} 님의 모드 버전이 다릅니다. ({theirs}, 필요한 버전 {required})"
        : $"Coop: {player} has mod version {theirs}. (needed: {required})";
}
