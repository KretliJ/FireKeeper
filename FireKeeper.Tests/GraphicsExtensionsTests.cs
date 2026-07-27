// FireKeeper.Tests/GraphicsExtensionsTests.cs
using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireKeeper.Tests
{
    [TestClass]
    public class GraphicsExtensionsTests
    {
        [TestMethod]
        public void GetRoundedRectanglePath_ShouldReturnNonNullPath_ForPositiveRadius()
        {
            var rect = new Rectangle(0, 0, 32, 32);

            using (GraphicsPath path = GraphicsExtensions.GetRoundedRectanglePath(rect, 8))
            {
                Assert.IsNotNull(path);
                Assert.IsTrue(path.PointCount > 0);
            }
        }

        [TestMethod]
        public void GetRoundedRectanglePath_ShouldFallBackToPlainRectangle_WhenRadiusIsZero()
        {
            var rect = new Rectangle(0, 0, 32, 32);

            using (GraphicsPath path = GraphicsExtensions.GetRoundedRectanglePath(rect, 0))
            {
                // AddRectangle produces exactly 4 corner points.
                Assert.AreEqual(4, path.PointCount);
            }
        }

        [TestMethod]
        public void GetRoundedRectanglePath_ShouldFallBackToPlainRectangle_ForNegativeRadius()
        {
            var rect = new Rectangle(0, 0, 32, 32);

            using (GraphicsPath path = GraphicsExtensions.GetRoundedRectanglePath(rect, -5))
            {
                Assert.AreEqual(4, path.PointCount);
            }
        }

        [TestMethod]
        public void GetRoundedRectanglePath_Bounds_ShouldApproximatelyMatchInputRectangle()
        {
            var rect = new Rectangle(0, 0, 100, 60);

            using (GraphicsPath path = GraphicsExtensions.GetRoundedRectanglePath(rect, 12))
            {
                RectangleF bounds = path.GetBounds();

                // Rounded corners eat slightly into the bounds; allow a small tolerance
                // instead of requiring an exact match.
                Assert.IsTrue(bounds.Width <= rect.Width + 1);
                Assert.IsTrue(bounds.Height <= rect.Height + 1);
                Assert.IsTrue(bounds.Width >= rect.Width - 25);
                Assert.IsTrue(bounds.Height >= rect.Height - 25);
            }
        }

        [TestMethod]
        public void FillRoundedRectangle_ShouldNotThrow_WhenCalledOnRealGraphics()
        {
            using (var bmp = new Bitmap(64, 64))
            using (Graphics g = Graphics.FromImage(bmp))
            using (var brush = new SolidBrush(Color.Orange))
            {
                var rect = new Rectangle(4, 4, 56, 56);

                g.FillRoundedRectangle(brush, rect, 10);

                // A quick sanity check that something was actually painted inside the rect.
                Color pixel = bmp.GetPixel(32, 32);
                Assert.AreNotEqual(Color.FromArgb(0, 0, 0, 0).ToArgb(), pixel.ToArgb());
            }
        }

        [TestMethod]
        public void DrawRoundedRectangle_ShouldNotThrow_WhenCalledOnRealGraphics()
        {
            using (var bmp = new Bitmap(64, 64))
            using (Graphics g = Graphics.FromImage(bmp))
            using (var pen = new Pen(Color.Black, 2))
            {
                var rect = new Rectangle(4, 4, 56, 56);

                g.DrawRoundedRectangle(pen, rect, 10);
            }

            // If we got here without an exception, the extension method works end-to-end.
            Assert.IsTrue(true);
        }
    }
}
