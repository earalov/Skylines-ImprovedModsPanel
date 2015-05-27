using ICities;

namespace ImprovedModsPanel
{

    public class Mod : IUserMod
    {

        public string Name
        {
            get
            {
                ImprovedModsPanel.Bootstrap();
                return "ImprovedModsPanel";
            }
        }

        public string Description
        {
            get { return "Redesigned mod list panel"; }
        }

    }

}
