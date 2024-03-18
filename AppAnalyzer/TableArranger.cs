using System;
using System.Collections.Generic;

public class TableArranger
{
    private class Cell
    {
        public string Contents { get; private set; }
        public int MaxLength { get; set; }
        public int WrapsNeeded { get; private set; }

        public Cell(string contents = "Cell", int maxLength = -1)
        {
            Contents = contents;
            MaxLength = maxLength;
            WrapsNeeded = (contents.Length <= maxLength) || (maxLength < 0)
                        ? 0 : (int) (contents.Length / maxLength);
        }
    }

    public int NumColumns { get; private set; }
    public int NumRows { get; private set; }
    public List<List<Cell>> Contents { get; set; }
    public string[] RawSeparators { get; set; }

    private List<int> _maxColumnSizes;
    private string _rowSeparator;
    private string _colSeparator;
    private string _cornerMarker;

    public TableArranger()
    {
        NumColumns = 0;
        NumRows = 0;
        Contents = new List<string>();
        RawSeparators = new string[] { " " };

        _maxColumnSizes = new List<int>();
        _rowSeparator = "-";
        _colSeparator = "|";
        _cornerMarker = "+";

        // We know that a table by definition will have headers, so we add the
        // placeholder from here.
        Contents.Add(new List<Cell>());
    }

    public TableArranger(string[] entries,
                         string[] headers,
                         int[] sizes,
                         string[] colSeparators) : this()
    {
        // Create the table here :)
    }

    public TableArranger(string[] entries,
                         string[] headers,
                         int[] sizes,
                         string colSeparator)
        : this(entries, headers, sizes, new string[] {colSeparator}) { }

    public void AddColumn(string colContents = "Column", int maxSize = -1)
    {
        Contents[0].Add(new Cell(colContents, maxSize));
        NumColumns++;
    }

    public void AddRow(string[] values)
    {
        // Here goes the core of adding contents to our table.
    }
}
