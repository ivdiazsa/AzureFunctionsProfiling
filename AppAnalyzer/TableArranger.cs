using System;
using System.Collections.Generic;
using System.Linq;

public class TableArranger
{
    public class Cell
    {
        // The text of the cell.
        public string Contents { get; private set; }

        // If we want to limit a certain column(s) to an x amount of characters,
        // then that is specified in this field. If this is set to a negative
        // number (e.g. the default is -1), then that is treated as a no-limit
        // set. This means the column would expand as necessary to fit the entirety
        // of the contents.
        public int MaxLength { get; set; }

        // Directly linked to the above field, if the contents of the cell exceed
        // the character limit set on MaxLength, then we need to wrap it and make
        // the cell use more lines. This field calculates how many more lines we
        // will need.
        public int WrapsNeeded { get; private set; }

        public Cell(string contents = "<Cell>", int maxLength = -1)
        {
            Contents = contents;
            MaxLength = maxLength;
            WrapsNeeded = (contents.Length <= maxLength) || (maxLength < 0)
                        ? 0 : (int) (contents.Length / maxLength);
        }
    }

    public int NumColumns { get; private set; }
    public int NumRows { get; private set; }

    // The representation of the table. It would usually be a 2D array, but in this
    // case, we're using a 2D List because we're also supporting adding new rows
    // and columns on the fly. Hence, we can't know beforehand the size of the
    // final table.
    public List<List<Cell>> Contents { get; set; }

    // private List<int> _maxColumnSizes;

    // These are just to customize how we want our table to look when we print it :)
    private string _rowSeparator;
    private string _colSeparator;
    private string _cornerMarker;

    public TableArranger()
    {
        NumColumns = 0;
        NumRows = 1;
        Contents = new List<List<Cell>>();

        // _maxColumnSizes = new List<int>();
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
                         string rawDelimiter = " ") : this()
    {
        CreateTableFromData(entries, headers, sizes, rawDelimiter);
    }

    public void AddColumn(string colContents = "<Column>", int maxSize = -1)
    {
        // The top row contains the headers, so we add it the cell with the name
        // of the column there.
        Contents[0].Add(new Cell(colContents, maxSize));
        NumColumns++;

        // If we're adding a column after we already have data, we have to update
        // each row with the new cell.
        for (int i = 1; i < Contents.Count; i++)
        {
            Contents[i].Add(new Cell());
        }
    }

    public void AddRow(string[] values, List<int> sizes)
    {
        List<Cell> row = new List<Cell>(values.Length);

        for (int i = 0; i < values.Length; i++)
        {
            row.Add(new Cell(values[i], sizes[i]));
        }

        Contents.Add(row);
    }

    private void CreateTableFromData(string[] entries,
                                     string[] headers,
                                     int[] sizes,
                                     string rawDelimiter)
    {
        List<string> headersList = headers.ToList();
        List<int> sizesList = sizes.ToList();
        CheckAndBalanceColumnsAndLengths(headersList, sizesList);

        // First add all the headers to the first row of the table, which has
        // already been created by the default constructor called with "this()".

        for (int i = 0; i < headersList.Count; i++)
        {
            AddColumn(headersList[i], sizesList[i]);
        }

        // This is the main core of creating a table from provided data. We read
        // each line, split it using the given raw delimiter, and then call AddRow(),
        // who takes care of creating the Cell objects and storing them into
        // the table.

        foreach (string rowData in entries)
        {
            string[] cellsData = rowData.Split(rawDelimiter);
            AddRow(cellsData, sizesList);
        }
    }

    private void CheckAndBalanceColumnsAndLengths(List<string> headers, List<int> sizes)
    {
        int numCols = headers.Count;
        int numColMaxLengths = sizes.Count;

        if (numCols == numColMaxLengths) return ;

        // In the case we get a mismatch in number of columns and number of lengths
        // we need to tell the user what will happen. Then, we have to balance
        // the lists.
        int difference = Math.Abs(numCols - numColMaxLengths);

        if (numCols < numColMaxLengths)
        {
            Console.WriteLine($"There are {difference} less columns than lengths"
                              + " provided. The last {difference} lengths will be"
                              + " ignored.");
            sizes.RemoveRange(sizes.Count - difference, difference);
        }
        else
        {
            Console.WriteLine($"There are {difference} less lengths than columns"
                              + " provided. The last {difference} columns will be"
                              + " set to have no character limit (i.e. -1).");
            sizes.AddRange(Enumerable.Repeat(-1, difference));
        }
    }
}
