using NFMWorld.DriverInterface;
using NFMWorld.Util;
using NImpeller;
using Silk.NET.Maths;

namespace NFMWorld;

internal class NImpellerGraphics(NImpellerBackend backend) : IGraphics
{
    private readonly ImpellerPaint _paint = ImpellerPaint.New()!;
    private readonly ImpellerTypographyContext _typographyContext = ImpellerTypographyContext.New()!;
    public Vector2D<float> Ratio { get; set; }

    public void SetColor(Color c)
    {
        _paint.SetColor(ImpellerColor.FromArgb(c.A, c.R, c.G, c.B));
    }

    public void FillPolygon(Span<int> xPoints, Span<int> yPoints, int nPoints)
    {
        if (nPoints <= 2)
        {
            return;
        }
        _paint.SetDrawStyle(ImpellerDrawStyle.kImpellerDrawStyleFill);
        
        using var pathBuilder = ImpellerPathBuilder.New()!;
        
        // calculate centroid
        float cx, cy;
        {
            int x = 0;
            int y = 0;
            for (int i = 0; i < nPoints; i++) {
                x += xPoints[i];
                y += yPoints[i];
            }
            cx = (float) x / nPoints;
            cy = (float) y / nPoints;
        }
        float sx = xPoints[0] < cx ? xPoints[0] - 0.5f : xPoints[0] + 0.5f;
        float sy = yPoints[0] < cy ? yPoints[0] - 0.5f : yPoints[0] + 0.5f;
        
        pathBuilder.MoveTo(new ImpellerPoint { X = sx, Y = sy });
        
        for (int i = 1; i < nPoints; i++) {
            int x = xPoints[i];
            int y = yPoints[i];
            if (xPoints[i - 1] == x && yPoints[i - 1] == y) continue;
            pathBuilder.LineTo(new ImpellerPoint
            {
                X = x < cx ? x - 0.5f : x + 0.5f,
                Y = y < cy ? y - 0.5f : y + 0.5f
            });
        }
        pathBuilder.LineTo(new ImpellerPoint { X = xPoints[0], Y = yPoints[0] });

        using var path = pathBuilder.CopyPathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;

        backend.DrawListBuilder?.DrawPath(path, _paint);
    }

    public void DrawPolygon(Span<int> xPoints, Span<int> yPoints, int nPoints)
    {
        if (nPoints <= 1 || (nPoints == 2 && xPoints[0] == xPoints[1] && yPoints[0] == yPoints[1]))
        {
            return;
        }
        
        _paint.SetDrawStyle(ImpellerDrawStyle.kImpellerDrawStyleStroke);

        using var pathBuilder = ImpellerPathBuilder.New()!;
        pathBuilder.MoveTo(new ImpellerPoint { X = xPoints[0], Y = yPoints[0] });
        
        for (int i = 1; i < nPoints; i++) {
            if (xPoints[i] == xPoints[i - 1] && yPoints[i] == yPoints[i - 1]) continue;
            pathBuilder.LineTo(new ImpellerPoint { X = xPoints[i], Y = yPoints[i] });
        }
        pathBuilder.LineTo(new ImpellerPoint { X = xPoints[0], Y = yPoints[0] });

        using var path = pathBuilder.CopyPathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;

        backend.DrawListBuilder?.DrawPath(path, _paint);
    }
    
    public void FillRect(int x1, int y1, int width, int height)
    {
        _paint.SetDrawStyle(ImpellerDrawStyle.kImpellerDrawStyleFill);
        backend.DrawListBuilder?.DrawRect(new ImpellerRect(x1, y1, width, height), _paint);
    }

    public void DrawLine(int x1, int y1, int x2, int y2)
    {
        _paint.SetDrawStyle(ImpellerDrawStyle.kImpellerDrawStyleStroke);
        backend.DrawListBuilder?.DrawLine(
            new ImpellerPoint { X = x1, Y = y1 },
            new ImpellerPoint { X = x2, Y = y2 },
            _paint
        );
    }

    public void SetAlpha(float f)
    {
        // throw new NotImplementedException();
    }

    public void DrawImage(IImage image, int x, int y)
    {
        throw new NotImplementedException();
    }

    public void SetFont(Font font)
    {
        
    }

    public IFontMetrics GetFontMetrics()
    {
        throw new NotImplementedException();
    }

    public void DrawString(string text, int x, int y)
    {
        using var paragraphBuilder = _typographyContext.ParagraphBuilderNew()!;
        using var impellerParagraphStyle = ImpellerParagraphStyle.New()!;
        impellerParagraphStyle.SetForeground(_paint);
        impellerParagraphStyle.SetFontSize(16f);
        paragraphBuilder.PushStyle(impellerParagraphStyle);
        paragraphBuilder.AddText(text);
        using var paragraph = paragraphBuilder.BuildParagraphNew(10000)!;
        backend.DrawListBuilder?.DrawParagraph(paragraph, new ImpellerPoint { X = x, Y = y });
    }

    public void FillOval(int p0, int p1, int p2, int p3)
    {
        
    }

    public void FillRoundRect(int x, int y, int wid, int hei, int arcWid, int arcHei)
    {
        
    }

    public void DrawRoundRect(int x, int y, int wid, int hei, int arcWid, int arcHei)
    {
        
    }

    public void DrawRect(int x1, int y1, int width, int height)
    {
        _paint.SetDrawStyle(ImpellerDrawStyle.kImpellerDrawStyleStroke);
        backend.DrawListBuilder?.DrawRect(new ImpellerRect(x1, y1, width, height), _paint);
    }

    public void DrawImage(IImage image, int x, int y, int width, int height)
    {
        throw new NotImplementedException();
    }
}