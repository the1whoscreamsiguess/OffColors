using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Menu.Remix.MixedUI;
using Menu.Remix.MixedUI.ValueTypes;
using UnityEngine;

namespace OffColors
{
    //remix menu
    public class RemixSettings : OptionInterface
    {
        public static Configurable<float> extravagance;
        public static Configurable<float> offColorPercentage;

        List<UIelement> elements;

        public RemixSettings()
        {
            extravagance = this.config.Bind<float>("extravagance", 0f);
            offColorPercentage = this.config.Bind<float>("offColorPercentage", 0.25f);

            elements = new List<UIelement>();
        }

        void Add(UIelement element, string description)
        {
            elements.Add(element);
            if (description != "")
            {
                element.description = description;
            }
            this.Tabs[0]._AddItem(element);
        }

        public override void Initialize()
        {
            base.Initialize();

            this.Tabs = new OpTab[]
                {
            new OpTab(this, "Options")
                };

            //big title text
            Add(new OpLabel(new UnityEngine.Vector2(0f, 500f), new UnityEngine.Vector2(600f, 100f), "Off Colors", FLabelAlignment.Center, true), "");

            Vector2 pos = new Vector2(250f, 350f);
            Vector2 posIncrement = new Vector2(0, -80f);
            Vector2 labelOffset = new Vector2(0f, 25f);

            

            string text = "Increase intensity of off colors (0 = default)";
            OpFloatSlider floatSlider = new OpFloatSlider(extravagance, pos, 100)
            {
                min = 0,
                max = 1
            };
            Add(floatSlider, text);
            Add(new OpLabel(pos + labelOffset, new Vector2(100f, 30f), "Extravagance", FLabelAlignment.Center, false), text);
            pos += posIncrement;

            text = "Average percentage of eligible creatures that will have off colors";
            floatSlider = new OpFloatSlider(offColorPercentage, pos, 100)
            {
                min = 0,
                max = 1
            };
            Add(floatSlider, text);
            Add(new OpLabel(pos + labelOffset, new Vector2(100f, 30f), "Percentage", FLabelAlignment.Center, false), text);
            pos += posIncrement;
        }
    }
}
