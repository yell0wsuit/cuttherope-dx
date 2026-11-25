using System.Collections.Generic;

using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// The row of page bullets under the Time Travel pack pages (iOS <c>BulletBar</c>): one
    /// bullet a page, the current page's drawn larger, every bullet centered on the row's height.
    /// </summary>
    internal sealed class TimeTravelPageBullets : BaseElement
    {
        private readonly List<Image> bullets = [];
        private readonly float gap;

        /// <summary>Initializes a new instance of the <see cref="TimeTravelPageBullets"/> class.</summary>
        /// <param name="count">Number of pages.</param>
        /// <param name="gap">Room between bullets, in asset pixels.</param>
        public TimeTravelPageBullets(int count, float gap)
        {
            this.gap = gap;
            for (int i = 0; i < count; i++)
            {
                Image bullet = Image.FromResource(Resources.Img.MenuPackSelectionTimeTravel, TimeTravelArt.PageBullet);
                bullet.anchor = 17;
                bullet.parentAnchor = 9;
                bullets.Add(bullet);
                _ = AddChild(bullet);
            }

            // Sized as iOS sizes it, for one larger bullet among the rest.
            Image current = Image.FromResource(Resources.Img.MenuPackSelectionTimeTravel, TimeTravelArt.PageBulletCurrent);
            Image plain = Image.FromResource(Resources.Img.MenuPackSelectionTimeTravel, TimeTravelArt.PageBullet);
            width = count <= 0 ? 0 : (int)(((count - 1) * (plain.width + gap)) + current.width);
            height = System.Math.Max(plain.height, current.height);
            SetCurrent(0);
        }

        /// <summary>Gets the page shown as current.</summary>
        public int Current { get; private set; } = -1;

        /// <summary>Shows a page as current and lays the row out again.</summary>
        /// <param name="page">Page to show; one out of range shows none.</param>
        public void SetCurrent(int page)
        {
            Current = page;
            float x = 0f;
            for (int i = 0; i < bullets.Count; i++)
            {
                Image bullet = bullets[i];
                bullet.SetDrawQuad(i == page ? TimeTravelArt.PageBulletCurrent : TimeTravelArt.PageBullet);
                bullet.x = x;
                bullet.y = height / 2f;
                x += bullet.width + gap;
            }
        }
    }
}
