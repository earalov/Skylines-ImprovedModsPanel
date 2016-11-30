using ICities;

namespace ImprovedModsPanel
{

    public class Mod : IUserMod
    {
        public string Name
        {
            get
            {
//                ImprovedModsPanel.Bootstrap();
                return "ImprovedModsPanel";
            }
        }

        public string Description => "Redesigned mod list panel";
    }

}
