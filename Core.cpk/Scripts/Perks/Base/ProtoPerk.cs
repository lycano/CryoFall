﻿namespace AtomicTorch.CBND.CoreMod.Perks.Base
{
    using System.Collections.Generic;
    using AtomicTorch.CBND.CoreMod.Characters.Player;
    using AtomicTorch.CBND.CoreMod.Systems.Creative;
    using AtomicTorch.CBND.CoreMod.Technologies;
    using AtomicTorch.CBND.GameApi.Data;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Resources;
    using AtomicTorch.GameEngine.Common.Extensions;

    public abstract class ProtoPerk : ProtoEntity
    {
        private List<TechNode> listedInTechNodes;

        public abstract ITextureResource Icon { get; }

        public void PrepareProtoSetLinkWithTechNode(TechNode techNode)
        {
            if (this.listedInTechNodes == null)
            {
                this.listedInTechNodes = new List<TechNode>();
            }

            this.listedInTechNodes.AddIfNotContains(techNode);
        }

        public bool SharedIsPerkUnlocked(ICharacter character, bool allowIfAdmin = true)
        {
            if (this.listedInTechNodes == null
                || this.listedInTechNodes.Count == 0)
            {
                // not unlockable perk!
                return false;
            }

            if (allowIfAdmin
                && CreativeModeSystem.SharedIsInCreativeMode(character))
            {
                return true;
            }

            var techs = character.SharedGetTechnologies();
            foreach (var node in this.listedInTechNodes)
            {
                if (techs.SharedIsNodeUnlocked(node))
                {
                    return true;
                }
            }

            return false;
        }
    }
}