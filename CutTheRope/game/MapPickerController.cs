using System.Collections.Generic;
using System.Xml.Linq;

using CutTheRope.iframework;
using CutTheRope.iframework.core;
using CutTheRope.iframework.visual;
using CutTheRope.ios;

namespace CutTheRope.game
{
    internal sealed class MapPickerController : ViewController, IButtonDelegation
    {
        public override NSObject InitWithParent(ViewController p)
        {
            if (base.InitWithParent(p) != null)
            {
                selectedMap = null;
                maplist = null;
                CreatePickerView();
                View view = new();
                RectangleElement rectangleElement = new()
                {
                    color = RGBAColor.whiteRGBA,
                    width = (int)SCREEN_WIDTH,
                    height = (int)SCREEN_HEIGHT
                };
                _ = view.AddChild(rectangleElement);
                FontGeneric font = Application.GetFont(4);
                Text text = new Text().InitWithFont(font);
                text.SetString("Loading...");
                text.anchor = text.parentAnchor = 18;
                _ = view.AddChild(text);
                AddViewwithID(view, 1);
                SetNormalMode();
            }
            return this;
        }

        public void CreatePickerView()
        {
            View view = new();
            RectangleElement rectangleElement = new()
            {
                color = RGBAColor.whiteRGBA,
                width = (int)SCREEN_WIDTH,
                height = (int)SCREEN_HEIGHT
            };
            _ = view.AddChild(rectangleElement);
            FontGeneric font = Application.GetFont(4);
            Text text = new Text().InitWithFont(font);
            text.SetString("START");
            Text text2 = new Text().InitWithFont(font);
            text2.SetString("START");
            text2.scaleX = text2.scaleY = 1.2f;
            Button button = new Button().InitWithUpElementDownElementandID(text, text2, 0);
            button.anchor = button.parentAnchor = 34;
            button.delegateButtonDelegate = this;
            _ = view.AddChild(button);
            AddViewwithID(view, 0);
        }

        public override void Activate()
        {
            base.Activate();
            if (autoLoad)
            {
                string text = "maps/";
                string nsstring = selectedMap;
                string nSString = NSS(text + (nsstring?.ToString()));
                XElement mapElement = XElementExtensions.LoadContentXml(nSString.ToString());
                XmlLoaderFinishedWithfromwithSuccess(mapElement, nSString, mapElement != null);
                return;
            }
            ShowView(0);
            LoadList();
        }

        public static void LoadList()
        {
        }

        public override void Deactivate()
        {
            base.Deactivate();
        }

        public void XmlLoaderFinishedWithfromwithSuccess(XElement rootNode, string url, bool success)
        {
            if (rootNode != null)
            {
                CTRRootController ctrrootController = (CTRRootController)Application.SharedRootController();
                ctrrootController.SetMap(rootNode);
                ctrrootController.SetMapName(selectedMap);
                CTRRootController.SetMapsList(maplist);
                Deactivate();
            }
        }

        public void SetNormalMode()
        {
            autoLoad = false;
            ((CTRRootController)Application.SharedRootController()).SetPicker(true);
        }

        public void SetAutoLoadMap(string map)
        {
            autoLoad = true;
            ((CTRRootController)Application.SharedRootController()).SetPicker(false);
            selectedMap = (string)NSRET(map);
        }

        public void OnButtonPressed(int n)
        {
            if (n == 0)
            {
                LoadList();
            }
        }

        public override void Dealloc()
        {
            base.Dealloc();
        }

        private string selectedMap;

        private Dictionary<string, XElement> maplist;

        private bool autoLoad;
    }
}
