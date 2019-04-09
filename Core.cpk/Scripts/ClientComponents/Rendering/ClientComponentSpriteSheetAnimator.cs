﻿namespace AtomicTorch.CBND.CoreMod.ClientComponents.Rendering
{
    using System;
    using AtomicTorch.CBND.GameApi.Resources;
    using AtomicTorch.CBND.GameApi.Scripting.ClientComponents;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;

    public class ClientComponentSpriteSheetAnimator : ClientComponent
    {
        private double currentTime;

        private double frameDurationSeconds;

        private int frameIndex;

        private ITextureResource[] framesTextureResources;

        private IComponentSpriteRenderer spriteRenderer;

        private double totalDurationSeconds;

        public int FrameIndex => this.frameIndex;

        public int FramesCount => this.framesTextureResources.Length;

        //public ITextureResource[] FramesTextureResources => this.framesTextureResources;

        /// <summary>
        /// Generate textures from atlas.
        /// </summary>
        /// <param name="textureAtlasResource">Texture atlas.</param>
        /// <param name="columns">(optional)</param>
        /// <param name="rowsCount">(optional)</param>
        /// <param name="autoInverse">
        /// (optional) Enable this if you want to append reverse animation. Please note - first and last
        /// frame will be displayed twice.
        /// </param>
        /// <returns></returns>
        public static ITextureResource[] CreateAnimationFrames(
            ITextureAtlasResource textureAtlasResource,
            byte? columns = null,
            byte? rowsCount = null,
            byte? onlySpecificRow = null,
            bool autoInverse = false)
        {
            var atlasColumnsCount = columns ?? textureAtlasResource.AtlasSize.ColumnsCount;
            var atlasRowsCount = rowsCount ?? textureAtlasResource.AtlasSize.RowsCount;

            int chunksCount;
            if (onlySpecificRow.HasValue)
            {
                chunksCount = atlasColumnsCount;
            }
            else
            {
                chunksCount = atlasColumnsCount * atlasRowsCount;
            }

            var result = new ITextureResource[chunksCount * (autoInverse ? 2 : 1)];

            if (onlySpecificRow.HasValue)
            {
                var row = onlySpecificRow.Value;
                for (byte column = 0; column < atlasColumnsCount; column++)
                {
                    result[column] = textureAtlasResource.Chunk(column, row);
                }
            }
            else
            {
                for (byte row = 0; row < atlasRowsCount; row++)
                {
                    for (byte column = 0; column < atlasColumnsCount; column++)
                    {
                        result[row * atlasColumnsCount + column] = textureAtlasResource.Chunk(column, row);
                    }
                }
            }

            if (autoInverse)
            {
                for (var i = 0; i < chunksCount; i++)
                {
                    result[chunksCount + i] = result[chunksCount - i - 1];
                }
            }

            return result;
        }

        public void Reset()
        {
            this.currentTime = 0f;
        }

        public void Setup(
            IComponentSpriteRenderer spriteRenderer,
            ITextureResource[] framesTextureResources,
            double frameDurationSeconds)
        {
            if (framesTextureResources == null
                || framesTextureResources.Length == 0)
            {
                throw new Exception("Incorrect sprite sheet");
            }

            this.spriteRenderer = spriteRenderer;
            this.framesTextureResources = framesTextureResources;
            this.frameDurationSeconds = frameDurationSeconds;
            this.totalDurationSeconds = frameDurationSeconds * framesTextureResources.Length;

            this.Reset();

            this.Update(0f);
        }

        public override void Update(double deltaTime)
        {
            if (this.framesTextureResources == null)
            {
                throw new Exception("Sprite sheet animator is not setup");
            }

            this.currentTime += deltaTime;
            this.frameIndex = this.totalDurationSeconds > 0
                                  ? (int)(this.currentTime % this.totalDurationSeconds / this.frameDurationSeconds)
                                  : 0;
            this.spriteRenderer.TextureResource = this.framesTextureResources[this.frameIndex];
            //Api.Logger.WriteDev("Sprite animation frame: #" + frameIndex + " total duration: " + this.currentTime.ToString("F3"));
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            this.Reset();
        }
    }
}