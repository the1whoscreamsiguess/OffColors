using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using System.Security.Permissions;
using UnityEngine;
using LizardCosmetics;
using RWCustom;
using Watcher;

[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
namespace OffColors
{
    [BepInPlugin("t1ws.offcolors", "OffColors", "1.0.0")]
    public class OffColors : BaseUnityPlugin
    {
        static bool initiated;
        public static BepInEx.Logging.ManualLogSource MyLogger;

        public void Awake()
        {
            On.RainWorld.OnModsInit += new On.RainWorld.hook_OnModsInit(OffColors.Init);
        }

        static void Init(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig.Invoke(self);
            if (!initiated)
            {
                initiated = true;
                MachineConnector.SetRegisteredOI("t1ws.offcolors", new RemixSettings());
            }
        }

        public void OnEnable()
        {
            MyLogger = Logger;
            Hooks.OnEnable();
        }

        //apply a color modifier
        public static Color ApplyOffColor(Color originalColor, HSLColor offColor)
        {
            Vector3 origColor = Custom.RGB2HSL(originalColor);
            HSLColor newColor = new HSLColor(origColor.x, origColor.y, origColor.z);

            newColor.hue += offColor.hue;
            newColor.saturation = Mathf.Clamp01(newColor.saturation + offColor.saturation);
            newColor.lightness = Mathf.Clamp(newColor.lightness + offColor.lightness, 0.01f, 1);

            if (newColor.hue > 1) newColor.hue -= 1;
            if (newColor.hue < 0) newColor.hue += 1;

            return newColor.rgb;
        }

        //reverse engineer how lit up the head is from this sprite's original color
        public static float EstimateLightUp(LizardGraphics lGraphics, Color color)
        {
            return Mathf.InverseLerp(lGraphics.palette.blackColor.r, lGraphics.effectColor.r, color.r);
        }

        //do simple color on every applicable sprite belonging to a given cosmetic using EstimteLightUp()
        public static void DoLightupEstimationColoring(LizardCosmetics.Template self, RoomCamera.SpriteLeaser sLeaser)
        {
            for (int i = self.startSprite + (self.numberOfSprites - 1); i >= self.startSprite; i--)
            {
                LizardGraphics lGraphics = self.lGraphics;
                HSLColor? offColor = GetColorOffset(lGraphics, i - self.startSprite);
                if (offColor.HasValue && sLeaser.sprites[i].color != lGraphics.palette.blackColor)
                {
                    float headLightUp = EstimateLightUp(lGraphics, sLeaser.sprites[i].color);
                    sLeaser.sprites[i].color = Color.Lerp(Color.Lerp(lGraphics.palette.blackColor, ApplyOffColor(lGraphics.effectColor, offColor.Value), headLightUp), lGraphics.whiteCamoColor, lGraphics.whiteCamoColorAmount);
                }
            }
        }

        //failsafe for modded lizards because the extra sprite index is a weird number or something??
        public static HSLColor? GetColorOffset(LizardGraphics self, int index)
        {
            if (index >= Hooks.scaleColors[self].Length || index < 0)
            {
                return null;
            } else
            {
                return Hooks.scaleColors[self][index];
            }
        }

        public static bool ShouldDoColoringForThisCosmetic(LizardCosmetics.Template template)
        {
            return template != null && !(template is Antennae || template is SnowAccumulation || template is Whiskers || template is SkinkStripes || template is LizardRotGraphics);
        }

        //get cosmetic from a sprite index
        public static Template CorrespondingCosmeticForSprite(LizardGraphics self, int sprite)
        {
            for (int i = 0; i < self.cosmetics.Count; i++)
            {
                if (sprite >= self.cosmetics[i].startSprite && sprite < self.cosmetics[i].startSprite + self.cosmetics[i].numberOfSprites)
                {
                    return self.cosmetics[i];
                }
            }
            return null;
        }

      
    }
}