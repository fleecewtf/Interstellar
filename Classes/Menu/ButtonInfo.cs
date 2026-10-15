using System;

namespace Interstellar.Classes.Menu
{
    public class ButtonInfo
    {
        public string buttonText = "-";
        public string overlapText;
        public string inputHint;

        public string[] aliases;

        public string toolTip = "This button doesn't have a tooltip/tutorial.";

        public Action method;
        public Action postMethod;

        public Action enableMethod;
        public Action disableMethod;

        public bool enabled;
        public bool isTogglable = true;
        public bool label;
        public bool incremental;


        public bool excludeFromSearch;

        public string customBind;
        public string rebindKey;

        public bool isSetting;
        public Action<bool> cycleValue;

        internal bool firstFrame = true;
    }
}
