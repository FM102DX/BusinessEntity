using System.ComponentModel.DataAnnotations;
using BlazorServerWebLogger.Contracts;

namespace BlazorServerWebLogger.Data
{
    public class LoggerMainViewSettings
    {
        public string DisplayedCats { get; set; }
        public string NonDisplayedCats { get; set; }

        public string DisplayedMessageTypes { get; set; }
        public string NonDisplayedMessageTypes { get; set; }

        public bool LogGenerationIsOn { get; set; }
        public bool Equals(LoggerMainViewSettings other)
        {
            if (other == null) return false;

            return this.DisplayedCats == other.DisplayedCats &&
                   this.DisplayedMessageTypes == other.DisplayedMessageTypes &&
                   this.LogGenerationIsOn == other.LogGenerationIsOn;
        }

    }
}
