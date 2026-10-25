using System.Diagnostics;
using System.Drawing;

namespace DesignerCapture;

/// <summary>ffmpeg（gdigrab）で画面の範囲を録画する。</summary>
internal sealed class Recorder : IDisposable
{
    private readonly Process _ffmpeg;

    /// <summary>録画を始める。出力は可逆に近い中間ファイル（.mkv）。</summary>
    public Recorder(Rectangle area, string rawPath, int fps = 30)
    {
        int w = area.Width / 2 * 2, h = area.Height / 2 * 2;
        var args = $"-y -hide_banner -loglevel error -f gdigrab -framerate {fps} -draw_mouse 1 -offset_x {area.X} -offset_y {area.Y} -video_size {w}x{h} -i desktop "
                 + $"-c:v libx264 -preset ultrafast -crf 0 -pix_fmt yuv444p \"{rawPath}\"";
        _ffmpeg = Process.Start(new ProcessStartInfo("ffmpeg", args) { UseShellExecute = false, RedirectStandardInput = true })!;
        Thread.Sleep(1200);   // 録画が始まるまで待つ
    }

    public void Stop()
    {
        if (_ffmpeg.HasExited) return;
        Thread.Sleep(600);
        _ffmpeg.StandardInput.Write('q');
        _ffmpeg.StandardInput.Flush();
        if (!_ffmpeg.WaitForExit(20000)) _ffmpeg.Kill();
    }

    public void Dispose() => Stop();

    /// <summary>
    /// 中間ファイルを、説明書に載せる形式にする。.webp は短いアニメーション（ループ）、.mp4 は H.264 の動画。
    /// <paramref name="part"/> を指定すると、その範囲（録画した範囲の左上から）を切り取り、<paramref name="width"/> を指定すると縮小する。
    /// </summary>
    public static void Encode(string rawPath, string outPath, int? width = null, Rectangle? part = null)
    {
        string filters = Filters(width, part);
        string args = Path.GetExtension(outPath).ToLowerInvariant() switch
        {
            ".webp" => $"-y -hide_banner -loglevel error -i \"{rawPath}\" -vf \"{filters}fps=12\" -c:v libwebp_anim -lossless 0 -q:v 80 -compression_level 6 -loop 0 -an \"{outPath}\"",
            ".mp4" => $"-y -hide_banner -loglevel error -i \"{rawPath}\" -vf \"{filters}fps=30,format=yuv420p\" -c:v libx264 -preset slow -crf 24 -tune stillimage -movflags +faststart -an \"{outPath}\"",
            _ => throw new ArgumentException("出力は .webp か .mp4 にしてください: " + outPath),
        };
        Run(args, outPath);
    }

    /// <summary>動画の 1 コマを PNG にする（MP4 のポスター画像に使う）。<paramref name="at"/> は秒。</summary>
    public static void Frame(string rawPath, string pngPath, double at, int? width = null, Rectangle? part = null)
    {
        string filters = Filters(width, part).TrimEnd(',');
        string vf = filters.Length > 0 ? $"-vf \"{filters}\" " : "";
        // 負の値は、終わりからの秒数
        string seek = (at < 0 ? "-sseof " : "-ss ") + at.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        Run($"-y -hide_banner -loglevel error {seek} -i \"{rawPath}\" -frames:v 1 {vf}\"{pngPath}\"", pngPath);
    }

    private static string Filters(int? width, Rectangle? part)
    {
        string crop = part is { } r ? $"crop={r.Width / 2 * 2}:{r.Height / 2 * 2}:{r.X}:{r.Y}," : "";
        string scale = width.HasValue ? $"scale={width}:-2:flags=lanczos," : "";
        return crop + scale;
    }

    private static void Run(string args, string outPath)
    {
        using var p = Process.Start(new ProcessStartInfo("ffmpeg", args) { UseShellExecute = false })!;
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException("ffmpeg が失敗しました: " + outPath);
    }
}
