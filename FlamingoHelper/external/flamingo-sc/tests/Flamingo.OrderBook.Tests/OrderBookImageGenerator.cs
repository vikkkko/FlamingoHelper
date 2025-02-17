using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using SkiaSharp;

namespace Flamingo.OrderBook.Tests
{
    public static class OrderBookImageGenerator
    {
        public class PriceNode
        {
            public BigInteger BaseAmount;
            public BigInteger QuoteTotal;
            public BigInteger EmptiedCount;
            public BigInteger EmptiedCountSibling;
        }

        public class OrderBookGrid
        {
            public BigInteger TreeBitLength { get; set; }
            public List<OrderBookCell> Cells { get; } = new List<OrderBookCell>();
        }

        public class OrderBookCell
        {
            public BigInteger NodeIndex { get; set; }
            public PriceNode PriceNode { get; set; }
            public bool Highlight { get; set; }
            public int Column { get; set; }
            public int Row { get; set; }
        }

        public static OrderBookGrid GenerateOrderBookGrid(BigInteger treeBitLength, Func<int, int, BigInteger> getNodeIndex, Func<BigInteger, PriceNode> getPriceNode)
        {
            var grid = new OrderBookGrid {TreeBitLength = treeBitLength};
            var columns = treeBitLength;
            var rightMostNumberOfNodes = 1 << (int)treeBitLength;

            // Collect nodes by their nodeIndex and track their row spans
            var nodeDictionary = new Dictionary<BigInteger, (PriceNode node, int col, int row)>();

            for (int col = 0; col < columns; col++)
            {
                BigInteger numberOfNodesInColumn = 1 << (col + 1);
                var priceRangePerNode = rightMostNumberOfNodes / numberOfNodesInColumn;

                for (int row = 1; row <= numberOfNodesInColumn; row++)
                {
                    var priceRow = (int) (row * priceRangePerNode) - 1;

                    BigInteger nodeIndex = getNodeIndex(priceRow, col);
                    if (!nodeDictionary.ContainsKey(nodeIndex))
                    {
                        nodeDictionary[nodeIndex] = (getPriceNode(nodeIndex), col, priceRow);
                    }
                    else
                    {
                        nodeDictionary[nodeIndex] = (nodeDictionary[nodeIndex].node, col, priceRow);
                    }
                }
            }

            foreach (var entry in nodeDictionary)
            {
                var (priceNode, col, row) = entry.Value;
                grid.Cells.Add(new OrderBookCell
                {
                    NodeIndex = entry.Key,
                    PriceNode = priceNode,
                    Column = col,
                    Row = row,
                    Highlight = false
                });
            }

            return grid;
        }

        public static void ExportOrderBookGridImage(OrderBookGrid grid, string outputPath)
        {
            int cellWidth = 200;
            int cellHeight = 100;
            int margin = 0;
            int rows = 1 << (int) grid.TreeBitLength;
            int columns = (int) grid.TreeBitLength;
            int width = columns * (cellWidth + margin);
            int height = rows * (cellHeight + margin);

            using (var surface = SKSurface.Create(new SKImageInfo(width, height)))
            {
                var canvas = surface.Canvas;
                canvas.Clear(new SKColor(123, 104, 238));

                // Group cells by column
                var columnCells = new Dictionary<int, List<OrderBookCell>>();
                foreach (var cell in grid.Cells)
                {
                    if (!columnCells.ContainsKey(cell.Column))
                        columnCells[cell.Column] = new List<OrderBookCell>();
                    columnCells[cell.Column].Add(cell);
                }

                // Draw cells, ensuring each column fills the total height
                foreach (var col in columnCells.Keys)
                {
                    var cells = columnCells[col];
                    int cellsInColumn = cells.Count;
                    int totalHeightPerCell = height / cellsInColumn;

                    // Sort cells by their row in descending order (bottom to top)
                    cells.Sort((a, b) => b.Row.CompareTo(a.Row));

                    for (int i = 0; i < cells.Count; i++)
                    {
                        var cell = cells[i];
                        int x = cell.Column * (cellWidth + margin);
                        int y = i * totalHeightPerCell; // Stack cells from bottom to top

                        DrawNode(canvas, x, y, cellWidth, totalHeightPerCell, cell, cell.Highlight);
                    }
                }

                using (var image = surface.Snapshot())
                using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                using (var stream = File.OpenWrite(outputPath))
                {
                    data.SaveTo(stream);
                }
            }
        }

        private static void DrawNode(SKCanvas canvas, int x, int y, int width, int height, OrderBookCell cell, bool highlight)
        {
            var rect = new SKRect(x, y, x + width, y + height);
            var paint = new SKPaint
            {
                Color = highlight ? new SKColor(153, 153, 0) : new SKColor(128, 0, 128), // Highlight color or Purple
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(rect, paint);

            paint.Style = SKPaintStyle.Stroke;
            paint.Color = SKColors.White;
            canvas.DrawRect(rect, paint);

            // Draw nodeIndex
            paint.Style = SKPaintStyle.Fill;
            paint.TextSize = 12;
            paint.Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
            paint.IsAntialias = true;
            paint.Color = SKColors.White;
            canvas.DrawText($"{cell.NodeIndex} [{cell.Column}, {cell.Row}]", x + 5, y + 20, paint);

            // Draw bottom border for nodeIndex
            var borderPaint = new SKPaint {Style = SKPaintStyle.Stroke, Color = new SKColor(255, 255, 255, 80), StrokeWidth = 1, IsAntialias = true};
            canvas.DrawLine(x, y + 30, x + width, y + 30, borderPaint);

            // Draw remaining node data
            paint.Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal);
            paint.TextSize = 12;
            string text = $"BaseAmount: {cell.PriceNode.BaseAmount}\n" +
                          $"QuoteTotal: {cell.PriceNode.QuoteTotal}\n" +
                          $"EmptiedCount: {cell.PriceNode.EmptiedCount}\n" +
                          $"EmptiedCountSibling: {cell.PriceNode.EmptiedCountSibling}";

            using (var textPaint = new SKPaint {Color = SKColors.White, TextSize = 12})
            {
                var lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    canvas.DrawText(lines[i], x + 5, y + 45 + (i * 15), textPaint);
                }
            }
        }

        public static OrderBookGrid HighlightDifferences(OrderBookGrid grid1, OrderBookGrid grid2)
        {
            if (grid1.TreeBitLength != grid2.TreeBitLength)
                throw new ArgumentException("Grids must have the same tree bit length");

            var resultGrid = new OrderBookGrid {TreeBitLength = grid1.TreeBitLength};

            var grid1Cells = new Dictionary<BigInteger, OrderBookCell>();
            foreach (var cell in grid1.Cells)
            {
                grid1Cells[cell.NodeIndex] = cell;
            }

            foreach (var cell in grid2.Cells)
            {
                var newCell = new OrderBookCell
                {
                    NodeIndex = cell.NodeIndex,
                    PriceNode = cell.PriceNode,
                    Column = cell.Column,
                    Row = cell.Row,
                    Highlight = false
                };

                if (grid1Cells.TryGetValue(cell.NodeIndex, out var matchingCell))
                {
                    if (!AreCellsEqual(matchingCell, cell))
                    {
                        newCell.Highlight = true;
                    }
                }
                else
                {
                    newCell.Highlight = true;
                }

                resultGrid.Cells.Add(newCell);
            }

            return resultGrid;
        }

        private static bool AreCellsEqual(OrderBookCell cell1, OrderBookCell cell2)
        {
            return cell1.PriceNode.BaseAmount == cell2.PriceNode.BaseAmount &&
                   cell1.PriceNode.QuoteTotal == cell2.PriceNode.QuoteTotal &&
                   cell1.PriceNode.EmptiedCount == cell2.PriceNode.EmptiedCount &&
                   cell1.PriceNode.EmptiedCountSibling == cell2.PriceNode.EmptiedCountSibling;
        }
    }
}
