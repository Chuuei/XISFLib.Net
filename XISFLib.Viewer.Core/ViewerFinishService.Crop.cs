namespace XISFLib.Viewer;

public static partial class ViewerFinishService
{
    private static void TrimMosaicCorners(SampleReader reader, int sourceWidth, int colors,
        ref int left, ref int top, ref int right, ref int bottom, CancellationToken token)
    {
        int width = right - left, height = bottom - top;
        if (width < 256 || height < 256) return;
        int cellX = (width + 191) / 192, cellY = (height + 191) / 192;
        int columns = width / cellX, rows = height / cellY;
        // Only consider coverage cropping when a corner is missing; never crop around a subject.
        int emptyCorners = 0;
        foreach (var (x, y) in new[] { (left, top), (right - 1, top), (left, bottom - 1), (right - 1, bottom - 1) })
            if (!reader.HasContent(y * sourceWidth + x, colors)) emptyCorners++;
        if (emptyCorners == 0) return;
        var heights = new int[columns]; var stack = new int[columns + 1];
        int populatedCells = 0;
        long bestArea = 0; int bestX = 0, bestY = 0, bestW = 0, bestH = 0;
        for (int y = 0; y < rows; y++)
        {
            token.ThrowIfCancellationRequested();
            for (int x = 0; x < columns; x++)
            {
                int empty = 0;
                for (int sy = 0; sy < 3; sy++)
                for (int sx = 0; sx < 3; sx++)
                {
                    int px = left + x * cellX + sx * (cellX - 1) / 2;
                    int py = top + y * cellY + sy * (cellY - 1) / 2;
                    if (!reader.HasContent(py * sourceWidth + px, colors)) empty++;
                }
                heights[x] = empty >= 2 ? 0 : heights[x] + 1;
                if (empty < 2) populatedCells++;
            }
            int count = 0;
            for (int x = 0; x <= columns; x++)
            {
                int h = x == columns ? 0 : heights[x];
                while (count > 0 && heights[stack[count - 1]] > h)
                {
                    int rectangleHeight = heights[stack[--count]];
                    int start = count == 0 ? 0 : stack[count - 1] + 1;
                    int rectangleWidth = x - start, startY = y + 1 - rectangleHeight;
                    long area = (long)rectangleWidth * cellX * rectangleHeight * cellY;
                    if (area > bestArea && rectangleWidth * cellX >= width * 0.50 && rectangleHeight * cellY >= height * 0.50
                        && start <= columns / 2 && x > columns / 2 && startY <= rows / 2 && y >= rows / 2)
                    { bestArea = area; bestX = start; bestY = startY; bestW = rectangleWidth; bestH = rectangleHeight; }
                }
                if (x < columns) stack[count++] = x;
            }
        }
        // Judge retention against actual mosaic coverage, not its empty bounding box.
        // A large safety floor and the center/axis checks prevent a tight subject crop.
        if (bestArea < (long)populatedCells * cellX * cellY * 0.60
            || bestArea < (long)width * height * 0.35) return;
        right = left + (bestX + bestW) * cellX; bottom = top + (bestY + bestH) * cellY;
        left += bestX * cellX; top += bestY * cellY;
    }
}

