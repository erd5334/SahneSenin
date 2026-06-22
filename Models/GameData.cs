using System.Collections.Generic;

namespace SahneSenin.Models
{
    public class GameData
    {
        public Dictionary<string, List<string>> Artists { get; set; } = new();
        public List<Teacher> Teachers { get; set; } = new();
    }
}
