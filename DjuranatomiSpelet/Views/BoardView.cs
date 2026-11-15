// Djuranatomi Spelet
// Copyright (c) 2026 Torbjörn Nilsson och DSS Maja Blomqvist
// SPDX-License-Identifier: EUPL-1.2

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DjuranatomiSpelet.Core;

namespace DjuranatomiSpelet.Views;

/// <summary>
/// The animal image with the path of spaces drawn on top. Sized in image pixels — put it in a Viewbox to scale.
/// Used both by the game and by the board editor.
/// </summary>
public sealed class BoardView : Grid
{
    private readonly Image _image = new() { Stretch = Stretch.Fill };

    public BoardView()
    {
        Children.Add(_image);
        Children.Add(SpaceLayer);
        Children.Add(TokenLayer);
        SetSize(1600, 900);
    }

    /// <summary>Spaces are drawn here. Transparent background so the editor receives clicks everywhere on the image.</summary>
    public Canvas SpaceLayer { get; } = new() { Background = Brushes.Transparent };

    /// <summary>Player tokens are drawn here.</summary>
    public Canvas TokenLayer { get; } = new() { IsHitTestVisible = false };

    public Animal? Animal { get; private set; }
    public double PixelWidth { get; private set; }
    public double PixelHeight { get; private set; }

    /// <summary>Space radius in image pixels.</summary>
    public double Radius => (Animal?.SpaceSize ?? 0.033) * Math.Min(PixelWidth, PixelHeight);

    public void SetAnimal(Animal animal)
    {
        Animal = animal;
        RefreshImage();
        Redraw();
    }

    /// <summary>Call after the animal's image bytes have changed.</summary>
    public void RefreshImage()
    {
        var bitmap = LoadBitmap(Animal?.Image);
        _image.Source = bitmap;
        if (bitmap is not null)
            SetSize(bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        else
            SetSize(1600, 900);
    }

    private void SetSize(double w, double h)
    {
        PixelWidth = w;
        PixelHeight = h;
        Width = w;
        Height = h;
        SpaceLayer.Width = TokenLayer.Width = w;
        SpaceLayer.Height = TokenLayer.Height = h;
    }

    public static Bitmap? LoadBitmap(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return null;
        try
        {
            using var ms = new MemoryStream(bytes);
            return new Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }

    public Point ToPixel(BoardSpace s) => new(s.X * PixelWidth, s.Y * PixelHeight);

    public Point ToRelative(Point pixel) => new(
        Math.Clamp(pixel.X / PixelWidth, 0, 1),
        Math.Clamp(pixel.Y / PixelHeight, 0, 1));

    /// <summary>Index of the space under the given pixel position, or -1.</summary>
    public int HitTestSpace(Point pixel)
    {
        if (Animal is null) return -1;
        var r = Radius * 1.15;
        for (int i = Animal.Spaces.Count - 1; i >= 0; i--)
        {
            var p = ToPixel(Animal.Spaces[i]);
            var dx = p.X - pixel.X;
            var dy = p.Y - pixel.Y;
            if (Math.Sqrt(dx * dx + dy * dy) <= r) return i;
        }
        return -1;
    }

    /// <summary>Redraws track and spaces. <paramref name="selected"/> gets a highlight ring (editor).</summary>
    public void Redraw(int selected = -1)
    {
        SpaceLayer.Children.Clear();
        if (Animal is null) return;

        var spaces = Animal.Spaces;
        var r = Radius;

        if (spaces.Count > 1)
        {
            var points = spaces.Select(ToPixel).ToList();
            SpaceLayer.Children.Add(new Polyline
            {
                Points = points,
                Stroke = Palette.Outline,
                StrokeThickness = r * 0.45,
                StrokeJoin = PenLineJoin.Round,
                Opacity = 0.35,
                IsHitTestVisible = false
            });
            SpaceLayer.Children.Add(new Polyline
            {
                Points = points.ToList(),
                Stroke = Brushes.White,
                StrokeThickness = r * 0.2,
                StrokeDashArray = new AvaloniaList<double> { 2, 2 },
                StrokeJoin = PenLineJoin.Round,
                IsHitTestVisible = false
            });
        }

        for (int i = 0; i < spaces.Count; i++)
        {
            var p = ToPixel(spaces[i]);
            var part = spaces[i].Part;
            var partBrush = part is null ? Palette.EmptySpace : Palette.PartBrush(part);
            bool isStart = i == 0, isGoal = i == spaces.Count - 1 && spaces.Count > 1;

            if (i == selected)
                AddCircle(p, r * 1.45, Brushes.Transparent, Palette.Brush("#FFD60A"), r * 0.3);

            if (isStart)
            {
                AddCircle(p, r * 1.25, Brushes.White, partBrush, r * 0.18);
                AddText(p, "START", r * 0.48, Palette.Outline, r * 4);
            }
            else if (isGoal)
            {
                SpaceLayer.Children.Add(new Polygon
                {
                    Points = Star(p, r * 1.7, r * 0.8),
                    Fill = Palette.Brush("#FFD60A"),
                    Stroke = partBrush,
                    StrokeThickness = r * 0.18,
                    StrokeJoin = PenLineJoin.Round,
                    IsHitTestVisible = false
                });
                AddText(new Point(p.X, p.Y + r * 0.08), "MÅL", r * 0.5, Palette.Outline, r * 4);
            }
            else
            {
                AddCircle(p, r, partBrush, Palette.Outline, r * 0.13);
                AddText(p, i.ToString(), r * 0.7, part is null ? Palette.Outline : Brushes.White, r * 2);
            }
        }
    }

    private void AddCircle(Point center, double r, IBrush fill, IBrush stroke, double thickness)
    {
        var e = new Ellipse { Width = r * 2, Height = r * 2, Fill = fill, Stroke = stroke, StrokeThickness = thickness, IsHitTestVisible = false };
        Canvas.SetLeft(e, center.X - r);
        Canvas.SetTop(e, center.Y - r);
        SpaceLayer.Children.Add(e);
    }

    /// <summary>Text centred on a point, using a fixed-size box (no text measuring needed).</summary>
    private void AddText(Point center, string text, double size, IBrush brush, double boxWidth)
    {
        var boxHeight = Math.Max(size * 2, 12);
        var box = new Grid { Width = boxWidth, Height = boxHeight, IsHitTestVisible = false };
        box.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = Math.Max(size, 6),
            FontWeight = FontWeight.Black,
            Foreground = brush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        Canvas.SetLeft(box, center.X - boxWidth / 2);
        Canvas.SetTop(box, center.Y - boxHeight / 2);
        SpaceLayer.Children.Add(box);
    }

    private static List<Point> Star(Point c, double outer, double inner)
    {
        var pts = new List<Point>();
        for (int i = 0; i < 10; i++)
        {
            var radius = i % 2 == 0 ? outer : inner;
            var angle = -Math.PI / 2 + i * Math.PI / 5;
            pts.Add(new Point(c.X + radius * Math.Cos(angle), c.Y + radius * Math.Sin(angle)));
        }
        return pts;
    }
}
