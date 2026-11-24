using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using RWCustom;
using LizardCosmetics;

namespace OffColors
{
    public static class Hooks
    {
        public static Dictionary<LizardGraphics, HSLColor?[]> scaleColors;

        public static void OnEnable()
        {
            On.RainWorldGame.ctor += RainWorldGame_ctor;
            On.LizardGraphics.ctor += LizardGraphics_ctor;
            On.LizardGraphics.ApplyPalette += LizardGraphics_ApplyPalette;

            On.LizardCosmetics.AxolotlGills.DrawSprites += AxolotlGills_DrawSprites;
            On.LizardCosmetics.BodyStripes.DrawSprites += BodyStripes_DrawSprites;
            On.LizardCosmetics.BumpHawk.DrawSprites += BumpHawk_DrawSprites;
            On.LizardCosmetics.JumpRings.DrawSprites += JumpRings_DrawSprites;
            On.LizardCosmetics.LongHeadScales.DrawSprites += LongHeadScales_DrawSprites;
            On.LizardCosmetics.ShortBodyScales.DrawSprites += ShortBodyScales_DrawSprites;
            On.LizardCosmetics.TailGeckoScales.DrawSprites += TailGeckoScales_DrawSprites;
        }

        //generate color offsets
        public static void LizardGraphics_ctor(On.LizardGraphics.orig_ctor orig, LizardGraphics self, PhysicalObject ow)
        {
            orig(self, ow);
            if (!scaleColors.ContainsKey(self))
            {
                scaleColors.Add(self, new HSLColor?[self.extraSprites]);

                int extraSprites = self.extraSprites;
                for (int i = 0; i < self.cosmetics.Count; i++)
                {
                    if (!OffColors.ShouldDoColoringForThisCosmetic(self.cosmetics[i]))
                    {
                        extraSprites -= self.cosmetics[i].numberOfSprites;
                    }
                }

                if (extraSprites > 0)
                {
                    UnityEngine.Random.State state = UnityEngine.Random.state;
                    UnityEngine.Random.InitState(self.lizard.abstractCreature.ID.RandomSeed);

                    float dominance = self.lizard.abstractCreature.personality.dominance;
                    if (UnityEngine.Random.value > RemixSettings.offColorPercentage.Value + (Mathf.Lerp(dominance, UnityEngine.Random.value, 0.5f) * RemixSettings.offColorPercentage.Value * 3))
                    {
                        return;
                    }

                    //higher dominance = higher chance of being extra flashy
                    float colorExtravagance = Mathf.Lerp(Mathf.Pow(UnityEngine.Random.value, 2 - dominance), 1, RemixSettings.extravagance.Value);

                    float adjustedHalfChance = 0.25f + (colorExtravagance * 0.25f);

                    float offHueChance = Mathf.Max(10f / extraSprites, Mathf.Pow(
            UnityEngine.Random.value * Mathf.Lerp(1f / (extraSprites / 10f), 1, UnityEngine.Random.value * colorExtravagance * 3),
    2 - (colorExtravagance * 1.9f)));

                    //most will only have one or two off shades but can theoretically go up to 7 under extraordinary circumstances
                    //color extravagance and number of colored scales both influence number of hues
                    HSLColor[] offShades = new HSLColor[(UnityEngine.Random.value < (adjustedHalfChance + (offHueChance / 2)) ? 2 : 1) + Mathf.FloorToInt(Mathf.Pow(UnityEngine.Random.value, 3 - ((colorExtravagance + offHueChance) * 1.25f)) * 5)];

                    for (int i = 0; i < offShades.Length; i++)
                    {
                        offShades[i] = new HSLColor();
                        //hue will normally be close to the original but can rarely go outside that range
                        //lower total color count = higher chance of an altered hue - simpler variants will be more noticeable and complex ones will have a more mottled appearance
                        if (UnityEngine.Random.value < (1f / (offShades.Length / 2f)) * adjustedHalfChance * 1.5f)
                        {
                            offShades[i].hue = UnityEngine.Random.value * (UnityEngine.Random.value < 0.5f ? -1 : 1) * (0.05f + (colorExtravagance * 0.95f));
                        }


                        if (UnityEngine.Random.value < adjustedHalfChance * 2f)
                        {
                            //saturation more likely to be higher than lower
                            if (UnityEngine.Random.value < adjustedHalfChance)
                            {
                                offShades[i].saturation = Mathf.Pow(UnityEngine.Random.value, 4) * -1;
                            }
                            else
                            {
                                offShades[i].saturation = Mathf.Pow(UnityEngine.Random.value, 2);
                            }
                        }

                        if (UnityEngine.Random.value < 0.5f * (adjustedHalfChance * 2) || (offShades[i].saturation == 0 && offShades[i].hue == 0))
                        {
                            bool light = UnityEngine.Random.value < 0.33f;

                            //extravagant colors have a slight trend towards neon colors
                            Vector3 effectHSL = Custom.RGB2HSL(OffColors.ApplyOffColor(self.effectColor, offShades[i]));
                            float lightnessToFullBright = (1 - (effectHSL.y / 2)) - effectHSL.z;

                            //off lightness is usually darker
                            offShades[i].lightness = Mathf.Lerp(UnityEngine.Random.value * (UnityEngine.Random.value * (light ? 0.5f : 2f)) * (light ? 1 : -1), lightnessToFullBright, Mathf.Pow(UnityEngine.Random.value, 3f - (colorExtravagance * 1.5f)));
                        }
                    }


                    for (int i = 0; i < self.extraSprites; i++)
                    {
                        if (UnityEngine.Random.value < offHueChance)
                        {
                            scaleColors[self][i] = offShades[UnityEngine.Random.Range(0, offShades.Length - 1)];
                        }
                    }

                    UnityEngine.Random.state = state;
                }

            }
        }

        //simple coloring
        public static void LizardGraphics_ApplyPalette(On.LizardGraphics.orig_ApplyPalette orig, LizardGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            orig(self, sLeaser, rCam, palette);
            if (self.extraSprites > 0)
            {
                //track the last cosmetic we were on so that we're not running the full identification check for every single sprite. this is a tiny bit more performant
                Template currentCosmetic = null;
                bool lastDoColor = false;
                for (int i = self.startOfExtraSprites; i < self.startOfExtraSprites + self.extraSprites; i++)
                {
                    bool doColor = false;
                    if (currentCosmetic != null && i >= currentCosmetic.startSprite && i < currentCosmetic.startSprite + currentCosmetic.numberOfSprites)
                    {
                        doColor = lastDoColor;
                    }
                    else
                    {
                        currentCosmetic = OffColors.CorrespondingCosmeticForSprite(self, i);
                        lastDoColor = OffColors.ShouldDoColoringForThisCosmetic(currentCosmetic);
                        doColor = lastDoColor;
                    }
                    if (doColor && sLeaser.sprites[i].color != sLeaser.sprites[self.SpriteBodyMesh].color)
                    {
                            HSLColor? data = OffColors.GetColorOffset(self, i - self.startOfExtraSprites);
                            if (data.HasValue)
                            {
                                sLeaser.sprites[i].color = OffColors.ApplyOffColor(sLeaser.sprites[i].color, data.Value);
                            }
                    }
                }
            }
        }

        //dictionary initializing
        public static void RainWorldGame_ctor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
        {
            orig(self, manager);
            scaleColors = new Dictionary<LizardGraphics, HSLColor?[]>();
        }

        //COLOR MATCHING FOR SPECIFIC COSMETIC TYPES

        public static void AxolotlGills_DrawSprites(On.LizardCosmetics.AxolotlGills.orig_DrawSprites orig, AxolotlGills self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);
            if (self.colored)
            {
                for (int i = self.startSprite + self.scalesPositions.Length - 1; i >= self.startSprite; i--)
                {
                    HSLColor? data = OffColors.GetColorOffset(self.lGraphics, (i + self.scalesPositions.Length) - self.startSprite);
                    if (data.HasValue)
                    {
                        sLeaser.sprites[i + self.scalesPositions.Length].color = OffColors.ApplyOffColor(sLeaser.sprites[i + self.scalesPositions.Length].color, data.Value);
                    }
                }
            }
        }

        public static void BodyStripes_DrawSprites(On.LizardCosmetics.BodyStripes.orig_DrawSprites orig, BodyStripes self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);

            for (int i = self.startSprite + self.scalesPositions.Length - 1; i >= self.startSprite; i--)
            {
                HSLColor? data = OffColors.GetColorOffset(self.lGraphics, (i - self.startSprite) - (i % 2 == 0 ? 0 : 1));
                if (data.HasValue)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        if (j > 1)
                        {
                            (sLeaser.sprites[i] as TriangleMesh).verticeColors[j] = OffColors.ApplyOffColor(self.lGraphics.effectColor, data.Value);
                        }
                        else
                        {
                            (sLeaser.sprites[i] as TriangleMesh).verticeColors[j] = self.lGraphics.BodyColor(timeStacker);
                        }
                    }
                }
            }
        }

        public static void BumpHawk_DrawSprites(On.LizardCosmetics.BumpHawk.orig_DrawSprites orig, BumpHawk self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);
            if (self.coloredHawk)
            {
                OffColors.DoLightupEstimationColoring(self, sLeaser);
            }
        }

        public static void JumpRings_DrawSprites(On.LizardCosmetics.JumpRings.orig_DrawSprites orig, JumpRings self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);
            OffColors.DoLightupEstimationColoring(self, sLeaser);
        }

        public static void LongHeadScales_DrawSprites(On.LizardCosmetics.LongHeadScales.orig_DrawSprites orig, LongHeadScales self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);
            if (self.colored)
            {
                for (int i = self.startSprite + self.scalesPositions.Length - 1; i >= self.startSprite; i--)
                {
                    HSLColor? data = OffColors.GetColorOffset(self.lGraphics, (i + self.scalesPositions.Length) - self.startSprite);
                    if (data.HasValue)
                    {
                        sLeaser.sprites[i + self.scalesPositions.Length].color = OffColors.ApplyOffColor(sLeaser.sprites[i + self.scalesPositions.Length].color, data.Value);
                    }
                }
            }
        }

        public static void ShortBodyScales_DrawSprites(On.LizardCosmetics.ShortBodyScales.orig_DrawSprites orig, ShortBodyScales self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);
            OffColors.DoLightupEstimationColoring(self, sLeaser);
        }

        public static void TailGeckoScales_DrawSprites(On.LizardCosmetics.TailGeckoScales.orig_DrawSprites orig, TailGeckoScales self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);
            if (self.bigScales)
            {
                LizardGraphics.LizardSpineData lizardSpineData = self.lGraphics.SpinePosition(0.4f, timeStacker);
                for (int i = 0; i < self.rows; i++)
                {
                    float num = Mathf.InverseLerp(0f, (float)(self.rows - 1), (float)i);
                    float num2 = Mathf.Lerp(0.5f, 0.99f, Mathf.Pow(num, 0.8f));
                    LizardGraphics.LizardSpineData lizardSpineData2 = self.lGraphics.SpinePosition(num2, timeStacker);
                    Color a = self.lGraphics.BodyColor(num2);
                    for (int j = 0; j < self.lines; j++)
                    {
                        float num3 = ((float)j + ((i % 2 == 0) ? 0.5f : 0f)) / (float)(self.lines - 1);
                        num3 = -1f + 2f * num3;
                        num3 += Mathf.Lerp(self.lGraphics.lastDepthRotation, self.lGraphics.depthRotation, timeStacker);
                        if (num3 < -1f)
                        {
                            num3 += 2f;
                        }
                        else if (num3 > 1f)
                        {
                            num3 -= 2f;
                        }
                        Vector2 vector = lizardSpineData.pos + lizardSpineData.perp * ((lizardSpineData.rad + 0.5f) * num3);
                        Vector2 vector2 = lizardSpineData2.pos + lizardSpineData2.perp * ((lizardSpineData2.rad + 0.5f) * num3);

                        HSLColor? data = OffColors.GetColorOffset(self.lGraphics, i * self.lines + j);
                        if (data.HasValue)
                        {
                            Color offColor = OffColors.ApplyOffColor(self.lGraphics.effectColor, data.Value);
                            if (self.lGraphics.iVars.tailColor > 0f)
                            {
                                float num4 = Mathf.InverseLerp(0.5f, 1f, Mathf.Abs(Vector2.Dot(Custom.DirVec(vector2, vector), Custom.DegToVec(-45f + 120f * num3))));
                                num4 = Custom.LerpMap(Mathf.Abs(num3), 0.5f, 1f, 0.3f, 0f) + 0.7f * Mathf.Pow(num4 * Mathf.Pow(self.lGraphics.iVars.tailColor, 0.3f), Mathf.Lerp(2f, 0.5f, num));
                                if (num < 0.5f)
                                {
                                    num4 *= Custom.LerpMap(num, 0f, 0.5f, 0.2f, 1f);
                                }
                                num4 = Mathf.Pow(num4, Mathf.Lerp(2f, 0.5f, num));
                                if (num4 < 0.5f)
                                {
                                    sLeaser.sprites[self.startSprite + i * self.lines + j].color = Color.Lerp(a, offColor, Mathf.InverseLerp(0f, 0.5f, num4));
                                }
                                else
                                {
                                    sLeaser.sprites[self.startSprite + i * self.lines + j].color = Color.Lerp(offColor, Color.white, Mathf.InverseLerp(0.5f, 1f, num4));
                                }
                            }
                            else
                            {
                                sLeaser.sprites[self.startSprite + i * self.lines + j].color = Color.Lerp(a, offColor, Custom.LerpMap(num, 0f, 0.8f, 0.2f, Custom.LerpMap(Mathf.Abs(num3), 0.5f, 1f, 0.8f, 0.4f), 0.8f));
                            }
                        }
                    }
                }
            }
            else
            {
                OffColors.DoLightupEstimationColoring(self, sLeaser);
            }
        }

    }
}
