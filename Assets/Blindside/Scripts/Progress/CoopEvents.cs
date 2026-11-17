// The co-op events: FLARE's story with each operator, one scene per rank-up (the scene of rank r takes the co-op from r to r + 1). A scene
// opens with an invitation the player may turn down (nothing happens, it stays on offer), then plays its lines — the operator's, FLARE's,
// an unseen voice's (???), narration — with choices whose answers can earn extra points, and raises the rank at its RankUp step (the
// rank-up card). Scene r ≥ 1 opens CoopGap after the scene before it (Campaign.CoopEventDue). While an operator has a scene written for its
// next rank, the other ways of earning points stop just short of it (Campaign.AddCoop): ranks are reached through the story.
// Played at the HQ by Hub.CoopEventPlayer. The text is the game's (Japanese only).
using System.Collections.Generic;

namespace Blindside.Progress
{
    public sealed class CoopEvent
    {
        public enum Kind { Line, Ask, RankUp }

        public sealed class Answer
        {
            public string label;
            public string[] reply;      // the speaker's answer, line by line (may be empty)
            public int bonus;           // extra co-op points (applied after the rank-up)
        }

        public sealed class Step
        {
            public Kind kind;
            public string speaker;      // an operator key, FLARE, Unknown (???) or null (narration)
            public string text;
            public Answer[] answers;
        }

        public string key, title;
        public int rank;                // the rank it starts from (it raises the co-op to rank + 1)
        public string invite, accept, decline;
        public string[] inviteOptions = { "はい、大丈夫です", "今はちょっと難しそうです" };
        public readonly List<Step> steps = new List<Step>();

        // ---- building a scene ----
        public CoopEvent Say(string text) { steps.Add(new Step { kind = Kind.Line, speaker = key, text = text }); return this; }
        public CoopEvent Say(string speaker, string text) { steps.Add(new Step { kind = Kind.Line, speaker = speaker, text = text }); return this; }
        public CoopEvent Narrate(string text) { steps.Add(new Step { kind = Kind.Line, speaker = null, text = text }); return this; }
        public CoopEvent Ask(string text, params Answer[] answers) { steps.Add(new Step { kind = Kind.Ask, speaker = key, text = text, answers = answers }); return this; }
        public CoopEvent RankUp() { steps.Add(new Step { kind = Kind.RankUp }); return this; }

        public static Answer A(string label, params string[] reply) => new Answer { label = label, reply = reply };
        public static Answer A(int bonus, string label, params string[] reply) => new Answer { label = label, reply = reply, bonus = bonus };
    }

    public static class CoopEvents
    {
        /// <summary>The speaker key of a voice whose face isn't shown (「???」).</summary>
        public const string Unknown = "???";
        /// <summary>Extra points a good answer earns.</summary>
        public const int Bonus = 5;

        static List<CoopEvent> all;

        /// <summary>The scene that takes `key`'s co-op from `rank` to rank + 1; null when none is written.</summary>
        public static CoopEvent Of(string key, int rank)
        {
            if (all == null) Build();
            foreach (var e in all) if (e.key == key && e.rank == rank) return e;
            return null;
        }

        public static bool Has(string key, int rank) => Of(key, rank) != null;

        static CoopEvent New(string key, int rank, string title, string invite, string accept, string decline)
        {
            var e = new CoopEvent { key = key, rank = rank, title = title, invite = invite, accept = accept, decline = decline };
            all.Add(e);
            return e;
        }

        static void Build()
        {
            all = new List<CoopEvent>();
            Crusher();
        }

        // ================================================================
        //  CRUSHER — the squad leader who lost his squad: harsh and strict, he lets no one close (a bond only gets in the way where
        //  everyone dies); little by little he opens up, and learns that comradeship is how people reach each other
        // ================================================================
        static void Crusher()
        {
            const string K = "CRUSHER";
            New(K, 0, "分隊面談",
                    "おい新入り、少し時間はあるか？", "よし。なら少し話そう。", "時間ができたら声をかけろ。")
                .Say("そう緊張するな。ただの分隊面談だ。")
                .Ask("ここまでやってみて、どうだ？\nきついことはあるか。",
                    CoopEvent.A("特にきつくはありません", "フン、嘘をつけ。"),
                    CoopEvent.A("とてもきついです", "軟弱だな。\n訓練の強度を上げるか。"))
                .Say("両親は田舎で暮らしていて……体力は人並み……")
                .Ask("まあ、こんなところか。\n面談はここまでだ。",
                    CoopEvent.A("もう終わりですか？", "なんだ？ 何か期待でもしていたのか？"),
                    CoopEvent.A("もっと何か聞かれると思ったのですが……", "なんだ？ 何か期待でもしていたのか？"))
                .Say("面談はただの面談だ。\n私的な話をするつもりはない。")
                .Say("任務に支障がないか確かめているだけだ。\n余計な期待はするな。")
                .Say("……まあ、これから同じ生活を送るチームとして、\nよろしく頼む。")
                .Say("FLARE", "……CRUSHERから、かすかな信頼を感じる。")
                .RankUp()
                .Say("よし、それじゃあ仕事に戻るか。")
                .Say("次の作戦でも、しっかり活躍してくれることを\n期待しているぞ、新入り。");

            New(K, 1, "命日",
                    "新入り、今回も少し時間はあるか？\n話がある。", "よし、ついて来い。", "分かった。また後でな。")
                .Say("今日は一緒に銃の手入れをする。")
                .Ask("銃はお前の命も同然だ。\nしっかり整備できるようにしておけ。",
                    CoopEvent.A("了解しました", "よし。手入れの道具は俺が取ってくる。\n待っていろ。"),
                    CoopEvent.A("面倒くさいな……", "銃より先に、\nお前の性根を整備する必要がありそうだな。"))
                .Narrate("――CRUSHERは道具を取りに行った……")
                .Narrate("――携帯電話が鳴っている……")
                .Say(Unknown, "お前に生きている資格なんてあるのか！！")
                .Say(Unknown, "私の息子を死なせておいて、よくも平気な顔で……！")
                .Say(Unknown, "どんな手を使ってでも……\nお前を不幸にしてやる！")
                .Narrate("――電話が切れた……")
                .Ask("ん？ 何か連絡でもあったのか？",
                    CoopEvent.A("……それが……"),
                    CoopEvent.A("知らない人が急に怒鳴ってきて……"))
                .Say("……分かった。礼を言う。")
                .Say("では、これから銃の手入れを始めるぞ。")
                .Narrate("――CRUSHERと一緒に銃を整備した。")
                .Ask("これくらいでいいだろう。\n新入りはどうだ？",
                    CoopEvent.A(Bonus, "完璧にやり遂げました", "フッ、自信ありげだな？\n念入りにチェックさせてもらおう。"),
                    CoopEvent.A("あれ、部品が一つ足りない……", "はぁ……。"))
                .Say("……さっきのは、俺の隊員の家族からの電話だ。")
                .Say("いや……今は、元隊員と言うべきだろうな。")
                .Say("今日が命日でな。\n時々、こうして電話がかかってくる。")
                .Ask("今日のことは……\nできれば内密にしてほしい。",
                    CoopEvent.A(Bonus, "分かりました", "助かる。"),
                    CoopEvent.A("考えておきます", "口は物理的に塞ぐこともできるんだが……\n試してみるか？"))
                .Say("とにかく……思いがけず、秘密ができてしまったな。")
                .Say("この秘密の共有関係、うまく保っていくとしよう。\nよろしく頼む。")
                .Narrate("――CRUSHERとの間に、説明しがたい信頼を感じる……")
                .RankUp()
                .Narrate("――また電話が鳴る……")
                .Say("ん……？ また電話か。少し失礼する。")
                .Say("なんだ、ただの迷惑電話じゃないか。");
        }
    }
}
