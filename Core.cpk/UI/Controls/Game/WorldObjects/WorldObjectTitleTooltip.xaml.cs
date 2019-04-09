﻿namespace AtomicTorch.CBND.CoreMod.UI.Controls.Game.WorldObjects
{
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;
    using AtomicTorch.GameEngine.Common.Client.MonoGame.UI;

    public partial class WorldObjectTitleTooltip : BaseUserControl
    {
        private static WorldObjectTitleTooltip lastControl;

        private IComponentAttachedControl attachedControl;

        public static void Hide()
        {
            lastControl?.attachedControl.Destroy();
            lastControl = null;
        }

        public static void ShowOn(IWorldObject worldObject, string message)
        {
            Hide();

            var staticWorldObject = worldObject as IStaticWorldObject;
            var protoStaticWorldObject = staticWorldObject?.ProtoStaticWorldObject;

            var positionOffset = protoStaticWorldObject != null
                                     ? protoStaticWorldObject.SharedGetObjectCenterWorldOffset(staticWorldObject)
                                     : (0, 0);

            positionOffset += (0, 1.18);

            lastControl = new WorldObjectTitleTooltip();
            lastControl.Setup(message);

            lastControl.attachedControl = Api.Client.UI.AttachControl(
                worldObject,
                lastControl,
                positionOffset: positionOffset,
                isFocusable: false);
        }

        public void Setup(string message)
        {
            this.DataContext = message;
        }

        protected override void InitControl()
        {
        }

        protected override void OnUnloaded()
        {
            if (ReferenceEquals(lastControl, this))
            {
                lastControl = null;
            }
        }
    }
};