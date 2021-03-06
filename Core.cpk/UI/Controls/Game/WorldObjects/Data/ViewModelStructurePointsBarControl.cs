﻿namespace AtomicTorch.CBND.CoreMod.UI.Controls.Game.WorldObjects.Data
{
    using AtomicTorch.CBND.CoreMod.ClientComponents.Timer;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Core;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.HUD.Data;
    using AtomicTorch.CBND.GameApi.Data.State;

    public class ViewModelStructurePointsBarControl : BaseViewModel
    {
        private StaticObjectStructurePointsData staticObjectStructurePointsData;

        // ReSharper disable once CanExtractXamlLocalizableStringCSharp
        public ViewModelHUDStatBar StatBar { get; }
            = new ViewModelHUDStatBar("Structure");

        public StaticObjectStructurePointsData StaticObjectStructurePointsData
        {
            get => this.staticObjectStructurePointsData;
            set
            {
                if (this.staticObjectStructurePointsData.Equals(value))
                {
                    return;
                }

                if (this.staticObjectStructurePointsData.State != null)
                {
                    this.ReleaseSubscriptions();
                }

                this.staticObjectStructurePointsData = value;

                if (value.State == null)
                {
                    return;
                }

                // set current values
                var state = value.State;
                this.StatBar.ValueCurrent = state.StructurePointsCurrent;
                this.StatBar.ValueMax = value.StructurePointsMax;

                // subscribe on updates
                state.ClientSubscribe(
                    _ => _.StructurePointsCurrent,
                    _ => this.StructurePointsCurrentUpdated(),
                    this);
            }
        }

        /// <summary>
        /// This method is required to set the initial value from which the value bar control will interpolate to current value.
        /// </summary>
        public void SetInitialStructurePoints(float sp)
        {
            this.StatBar.ValueCurrent = sp;
            // ensure that the current value will be displayed in the next frame
            ClientComponentTimersManager.AddAction(0, this.RefreshBar);
        }

        protected override void DisposeViewModel()
        {
            base.DisposeViewModel();
            this.StaticObjectStructurePointsData = default;
        }

        private void RefreshBar()
        {
            if (this.staticObjectStructurePointsData.State == null)
            {
                return;
            }

            this.StatBar.ValueCurrent = this.staticObjectStructurePointsData.State.StructurePointsCurrent;
        }

        private void StructurePointsCurrentUpdated()
        {
            this.RefreshBar();
        }
    }
}