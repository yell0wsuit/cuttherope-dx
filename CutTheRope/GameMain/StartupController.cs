using CutTheRope.Framework.Core;
using CutTheRope.Framework.Media;
using CutTheRope.Framework.Visual;

namespace CutTheRope.GameMain
{
    internal sealed class StartupController : ViewController, IResourceMgrDelegate, IMovieMgrDelegate
    {
        public StartupController(ViewController parent)
            : base(parent)
        {
            View view = new();
            Image image = Image.Image_createWithResID(Resources.Img.ZeptolabNoLink);
            image.parentAnchor = image.anchor = 18;
            image.scaleX = image.scaleY = 1.25f;
            _ = view.AddChild(image);
            bar = TiledImage.TiledImage_createWithResID(Resources.Img.LoaderbarFull);
            bar.parentAnchor = bar.anchor = 9;
            bar.SetTile(-1);
            bar.x = 738f;
            bar.y = 1056f;
            _ = image.AddChild(bar);
            barTotalWidth = bar.width;
            AddViewwithID(view, 1);
        }

        public override void Update(float t)
        {
            base.Update(t);
            float num = Application.SharedResourceMgr().GetPercentLoaded();
            bar.width = (int)(barTotalWidth * num / 100f);
        }

        public void MoviePlaybackFinished(string url)
        {
            CTRResourceMgr ctrresourceMgr = Application.SharedResourceMgr();
            ctrresourceMgr.resourcesDelegate = this;
            ctrresourceMgr.InitLoading();
            ctrresourceMgr.LoadPack(PackCommon);
            ctrresourceMgr.LoadPack(PackCommonImages);
            ctrresourceMgr.LoadPack(PackMenu);
            ctrresourceMgr.LoadPack(PackLocalizationMenu);
            ctrresourceMgr.LoadPack(PackMusic);
            ctrresourceMgr.StartLoading();
        }

        public override void Activate()
        {
            base.Activate();
            ShowView(1);
            MoviePlaybackFinished(null);
        }

        public void ResourceLoaded(int resName)
        {
        }

        public void AllResourcesLoaded()
        {
            Application.SharedRootController().SetViewTransition(4);
            Deactivate();
        }

        private readonly float barTotalWidth;

        private readonly TiledImage bar;

        private static readonly string[] PackCommon = ResourceNameTranslator.TranslateLegacyPack(PACK_COMMON);

        private static readonly string[] PackCommonImages = ResourceNameTranslator.TranslateLegacyPack(PACK_COMMON_IMAGES);

        private static readonly string[] PackMenu = ResourceNameTranslator.TranslateLegacyPack(PACK_MENU);

        private static readonly string[] PackLocalizationMenu = ResourceNameTranslator.TranslateLegacyPack(PACK_LOCALIZATION_MENU);

        private static readonly string[] PackMusic = ResourceNameTranslator.TranslateLegacyPack(PACK_MUSIC);
    }
}
