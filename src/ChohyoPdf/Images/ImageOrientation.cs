namespace ChohyoPdf.Images
{
    /// <summary>
    /// EXIF の Orientation（1〜8）に対応する、画像の向きの変換。画素データは変えず、描画時の変換行列だけで向きを直す。
    /// 1 = そのまま、2 = 左右反転、3 = 180 度回転、4 = 上下反転、5 = 転置（主対角線で反転）、
    /// 6 = 時計回りに 90 度回転、7 = 逆転置（副対角線で反転）、8 = 反時計回りに 90 度回転（時計回りに 270 度）。
    /// </summary>
    internal static class ImageOrientation
    {
        /// <summary>向きの値が 1〜8 の範囲か。</summary>
        public static bool IsValid(int orientation) => orientation >= 1 && orientation <= 8;

        /// <summary>表示したときに、縦と横が入れ替わる向き（5〜8）か。</summary>
        public static bool SwapsAxes(int orientation) => orientation >= 5 && orientation <= 8;

        /// <summary>
        /// 画像の XObject（単位正方形。左下が原点、v は上向き）を、表示する矩形（左上が (x, y)、幅 w、高さ h。y は下向き）に写す
        /// 変換行列 [a b c d e f] を返す。w と h は、向きを直した後の表示上の大きさ。
        /// 向きが 1 のときは、従来の [w 0 0 -h x y+h]。
        /// </summary>
        public static double[] GetMatrix(int orientation, double x, double y, double w, double h)
        {
            // 元の画像上の位置（sx: 右向き、sy: 下向き。0〜1）から表示上の位置（dx, dy。0〜1）への変換:
            //   dx = p*sx + q*sy + r,  dy = s*sx + t*sy + u
            double p, q, r, s, t, u;
            switch (orientation)
            {
                case 2: p = -1; q = 0; r = 1; s = 0; t = 1; u = 0; break;
                case 3: p = -1; q = 0; r = 1; s = 0; t = -1; u = 1; break;
                case 4: p = 1; q = 0; r = 0; s = 0; t = -1; u = 1; break;
                case 5: p = 0; q = 1; r = 0; s = 1; t = 0; u = 0; break;
                case 6: p = 0; q = -1; r = 1; s = 1; t = 0; u = 0; break;
                case 7: p = 0; q = -1; r = 1; s = -1; t = 0; u = 1; break;
                case 8: p = 0; q = 1; r = 0; s = -1; t = 0; u = 1; break;
                default: p = 1; q = 0; r = 0; s = 0; t = 1; u = 0; break;
            }
            // XObject の座標 (u, v) は sx = u、sy = 1 - v。表示の X = x + w*dx、Y = y + h*dy
            return new[]
            {
                w * p,                  // a
                h * s,                  // b
                -w * q,                 // c
                -h * t,                 // d
                x + w * (q + r),        // e
                y + h * (t + u),        // f
            };
        }
    }
}
