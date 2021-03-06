﻿namespace AtomicTorch.CBND.CoreMod.UI.Controls.Game.HUD.Notifications
{
    using System;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Animation;
    using AtomicTorch.CBND.CoreMod.SoundPresets;
    using AtomicTorch.CBND.CoreMod.Systems.Cursor;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.HUD.Notifications.Data;
    using AtomicTorch.CBND.GameApi.Resources;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.Scripting.ClientComponents;
    using AtomicTorch.GameEngine.Common.Client.MonoGame.UI;

    public partial class HUDNotificationControl : BaseUserControl
    {
        public SoundResource soundToPlay;

        private Border root;

        private Storyboard storyboardFadeOut;

        private Storyboard storyboardHide;

        private Storyboard storyboardShow;

        public bool IsAutoHide { get; private set; }

        public bool IsHiding { get; private set; }

        public ViewModelHUDNotificationControl ViewModel { get; private set; }

        public static HUDNotificationControl Create(
            string title,
            string message,
            Brush brushBackground,
            Brush brushForeground,
            ITextureResource icon,
            Action onClick,
            bool autoHide,
            SoundResource soundToPlay)
        {
            var iconBrush = icon != null
                                ? Api.Client.UI.GetTextureBrush(icon)
                                : null;

            return new HUDNotificationControl()
            {
                ViewModel = new ViewModelHUDNotificationControl(
                    title,
                    message,
                    brushBackground,
                    brushForeground,
                    iconBrush,
                    onClick),
                IsAutoHide = autoHide,
                soundToPlay = soundToPlay
            };
        }

        public void Hide(bool quick)
        {
            if (quick)
            {
                this.storyboardFadeOut.SpeedRatio = 6.5;
            }

            if (this.IsHiding)
            {
                // already hiding
                return;
            }

            this.IsHiding = true;

            if (!this.isLoaded)
            {
                this.RemoveControl();
                return;
            }

            this.storyboardShow.Stop();
            this.storyboardFadeOut.Begin();
        }

        public bool IsSame(HUDNotificationControl other)
        {
            return this.ViewModel.IsSame(other.ViewModel);
        }

        public void SetupAutoHideChecker(Func<bool> checker)
        {
            Api.Client.Scene.CreateSceneObject("Notification auto-hide checker")
               .AddComponent<ClientComponentNotificationAutoHideChecker>()
               .Setup(this, checker);
        }

        protected override void InitControl()
        {
            if (IsDesignTime)
            {
                this.ViewModel = new ViewModelHUDNotificationControl();
                return;
            }

            this.storyboardShow = this.GetResource<Storyboard>("StoryboardShow");
            this.storyboardHide = this.GetResource<Storyboard>("StoryboardHide");
            this.storyboardFadeOut = this.GetResource<Storyboard>("StoryboardFadeOut");
            this.root = this.GetByName<Border>("Border");
            this.DataContext = this.ViewModel;
        }

        protected override void OnLoaded()
        {
            this.UpdateLayout();
            this.ViewModel.RequiredHeight = (float)this.ActualHeight;

            if (IsDesignTime)
            {
                return;
            }

            this.storyboardFadeOut.Completed += this.StoryboardFadeOutCompletedHandler;
            this.storyboardHide.Completed += this.StoryboardHideCompletedHandler;
            this.root.MouseLeftButtonDown += this.RootMouseButtonLeftHandler;
            this.root.MouseRightButtonDown += this.RootMouseButtonRightHandler;
            this.root.MouseEnter += this.RootMouseEnterHandler;
            this.root.MouseLeave += this.RootMouseLeaveHandler;

            this.storyboardShow.Begin();

            if (this.soundToPlay != null)
            {
                Api.Client.Audio.PlayOneShot(this.soundToPlay,
                                             SoundConstants.VolumeUINotifications);
            }
        }

        protected override void OnUnloaded()
        {
            if (IsDesignTime)
            {
                return;
            }

            this.DataContext = null;
            this.ViewModel.Dispose();
            this.ViewModel = null;

            this.storyboardFadeOut.Completed -= this.StoryboardFadeOutCompletedHandler;
            this.storyboardHide.Completed -= this.StoryboardHideCompletedHandler;
            this.root.MouseLeftButtonDown -= this.RootMouseButtonLeftHandler;
            this.root.MouseRightButtonDown -= this.RootMouseButtonRightHandler;
            this.root.MouseEnter -= this.RootMouseEnterHandler;
            this.root.MouseLeave -= this.RootMouseLeaveHandler;

            // to ensure that the control has a hiding flag (used for ClientComponentNotificationAutoHideChecker)
            this.IsHiding = true;
        }

        private void RemoveControl()
        {
            ((Panel)this.Parent).Children.Remove(this);
        }

        private void RootMouseButtonLeftHandler(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            this.Hide(quick: true);
            this.ViewModel.CommandClick?.Execute(null);
        }

        private void RootMouseButtonRightHandler(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            this.Hide(quick: true);
        }

        private void RootMouseEnterHandler(object sender, MouseEventArgs e)
        {
            ClientCursorSystem.CurrentCursorId = this.ViewModel.Cursor;
        }

        private void RootMouseLeaveHandler(object sender, MouseEventArgs e)
        {
            ClientCursorSystem.CurrentCursorId = CursorId.Default;
        }

        private void StoryboardFadeOutCompletedHandler(object sender, EventArgs e)
        {
            this.storyboardHide.Begin();
        }

        private void StoryboardHideCompletedHandler(object sender, EventArgs e)
        {
            this.RemoveControl();
        }

        private class ClientComponentNotificationAutoHideChecker : ClientComponent
        {
            private Func<bool> checker;

            private HUDNotificationControl control;

            public void Setup(HUDNotificationControl control, Func<bool> checker)
            {
                this.control = control;
                this.checker = checker;
            }

            public override void Update(double deltaTime)
            {
                if (this.control.IsHiding)
                {
                    // checker is not required anymore
                    this.SceneObject.Destroy();
                    return;
                }

                if (!this.checker())
                {
                    return;
                }

                // auto hide check success - hide the notification
                this.control.Hide(quick: false);
                this.SceneObject.Destroy();
            }
        }
    }
}