using AssetStudio;
using AssetStudioCLI;
using AssetStudioCLI.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;

namespace Arknights
{
    internal static class AkSpriteHelper
    {
        public static Texture2D TryFindAlphaTex(AssetItem assetItem, AvgSprite avgSprite, bool isAvgSprite)
        {
            Sprite m_Sprite = (Sprite)assetItem.Asset;
            var imgType = "arts/characters";
            if (m_Sprite.m_RD.alphaTexture.m_PathID == 0)
            {
                if (isAvgSprite)
                {
                    if (avgSprite?.FullAlphaTexture != null)
                        return avgSprite.FullAlphaTexture;

                    imgType = "avg/characters";  //since the avg hub was not found for some reason, let's try to find alpha tex by name
                }
                var spriteFullName = Path.GetFileNameWithoutExtension(assetItem.Container);
                foreach (var item in Studio.loadedAssetsList)
                {
                    if (item.Type == ClassIDType.Texture2D)
                    {
                        if (item.Container.Contains(imgType) && item.Container.Contains($"illust_{m_Sprite.m_Name}_material") && item.Text.Contains("[alpha]"))
                            return (Texture2D)item.Asset;
                        else if (item.Container.Contains(imgType) && item.Container.Contains(spriteFullName) && item.Text == $"{m_Sprite.m_Name}[alpha]")
                            return (Texture2D)item.Asset;
                    }
                }
            }
            return null;
        }

        public static Image<Bgra32> AkGetImage(this Sprite m_Sprite, AvgSprite avgSprite = null, SpriteMaskMode spriteMaskMode = SpriteMaskMode.On)
        {
            if (m_Sprite.m_RD.texture.TryGet(out var m_Texture2D) && m_Sprite.m_RD.alphaTexture.TryGet(out var m_AlphaTexture2D) && spriteMaskMode != SpriteMaskMode.Off)
            {
                Image<Bgra32> tex;
                Image<Bgra32> alphaTex;

                if (avgSprite != null && avgSprite.IsHubParsed)
                {
                    alphaTex = m_AlphaTexture2D.ConvertToImage(true);
                    if (avgSprite.IsFaceSprite)
                    {
                        var faceImage = m_Texture2D.ConvertToImage(true);
                        var faceAlpha = avgSprite.FaceSpriteAlphaTexture.ConvertToImage(true);
                        if (faceImage.Size() != avgSprite.FaceSize)
                        {
                            faceImage.Mutate(x => x.Resize(new ResizeOptions { Size = avgSprite.FaceSize, Sampler = KnownResamplers.Lanczos3, Mode = ResizeMode.Stretch }));
                            faceAlpha.Mutate(x => x.Resize(new ResizeOptions { Size = avgSprite.FaceSize, Sampler = KnownResamplers.Lanczos3, Mode = ResizeMode.Stretch }));
                        }
                        tex = avgSprite.FullTexture.ConvertToImage(true);
                        tex.Mutate(x => x.DrawImage(faceImage, location: avgSprite.FacePos, opacity: 1f));
                        alphaTex.Mutate(x => x.DrawImage(faceAlpha, location: avgSprite.FacePos, opacity: 1f));
                    }
                    else
                    {
                        tex = m_Texture2D.ConvertToImage(true);
                    }
                }
                else
                {
                    tex = CutImage(m_Texture2D.ConvertToImage(false), m_Sprite.m_RD.textureRect, m_Sprite.m_RD.downscaleMultiplier);
                    alphaTex = CutImage(m_AlphaTexture2D.ConvertToImage(false), m_Sprite.m_RD.textureRect, m_Sprite.m_RD.downscaleMultiplier);
                }
                tex.ApplyRGBMask(alphaTex);
                return tex;
            }
            else if (m_Sprite.m_RD.texture.TryGet(out m_Texture2D))
            {
                return CutImage(m_Texture2D.ConvertToImage(false), m_Sprite.m_RD.textureRect, m_Sprite.m_RD.downscaleMultiplier);
            }

            return null;
        }

        private static void ApplyRGBMask(this Image<Bgra32> tex, Image<Bgra32> texMask)
        {
            using (texMask)
            {
                bool resized = false;
                if (tex.Width != texMask.Width || tex.Height != texMask.Height)
                {
                    texMask.Mutate(x => x.Resize(tex.Width, tex.Height, CLIOptions.o_akAlphaMaskResampler.Value));
                    resized = true;
                }

                var invGamma = 1.0 / (1.0 + CLIOptions.o_akAlphaMaskGamma.Value / 10.0);
                if (CLIOptions.akResizedOnly && !resized)
                {
                    invGamma = 1.0;
                }

                tex.ProcessPixelRows(texMask, (sourceTex, targetTexMask) =>
                {
                    for (int y = 0; y < texMask.Height; y++)
                    {
                        var texRow = sourceTex.GetRowSpan(y);
                        var maskRow = targetTexMask.GetRowSpan(y);
                        for (int x = 0; x < maskRow.Length; x++)
                        {
                            var grayscale = (maskRow[x].R + maskRow[x].G + maskRow[x].B) / 3.0;
                            if (invGamma != 1.0)
                            {
                                grayscale = 255 - Math.Pow((255 - grayscale) / 255, invGamma) * 255;
                            }
                            texRow[x].A = (byte)grayscale;
                        }
                    }
                });
            }
        }

        private static Image<Bgra32> CutImage(Image<Bgra32> originalImage, Rectf textureRect, float downscaleMultiplier)
        {
            if (originalImage != null)
            {
                using (originalImage)
                {
                    if (downscaleMultiplier > 0f && downscaleMultiplier != 1f)
                    {
                        var newSize = (Size)(originalImage.Size() / downscaleMultiplier);
                        originalImage.Mutate(x => x.Resize(newSize, KnownResamplers.Lanczos3, compand: true));
                    }
                    var rectX = (int)Math.Floor(textureRect.x);
                    var rectY = (int)Math.Floor(textureRect.y);
                    var rectRight = (int)Math.Ceiling(textureRect.x + textureRect.width);
                    var rectBottom = (int)Math.Ceiling(textureRect.y + textureRect.height);
                    rectRight = Math.Min(rectRight, originalImage.Width);
                    rectBottom = Math.Min(rectBottom, originalImage.Height);
                    var rect = new Rectangle(rectX, rectY, rectRight - rectX, rectBottom - rectY);
                    var spriteImage = originalImage.Clone(x => x.Crop(rect));
                    spriteImage.Mutate(x => x.Flip(FlipMode.Vertical));

                    return spriteImage;
                }
            }

            return null;
        }
    }
}
