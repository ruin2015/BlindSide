// The squad's real names (shown on the co-op screen under the codename): a name and its reading, by operator key.
namespace Blindside.Progress
{
    public static class OperatorProfiles
    {
        public static (string name, string reading) RealName(string key)
        {
            switch (key)
            {
                case "FLARE": return ("朝比奈 陽", "あさひな ひなた");
                case "CRUSHER": return ("鬼塚 剛", "おにづか ごう");
                case "IGNIS": return ("火野 蓮", "ひの れん");
                case "SERUM": return ("白石 葵", "しらいし あおい");
                case "BASTION": return ("岩城 大和", "いわき やまと");
                case "GHOST": return ("影山 透", "かげやま とおる");
                case "SILENCE": return ("早乙女 静", "さおとめ しずか");
            }
            return ("不明", "");
        }
    }
}
