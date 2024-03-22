using System;
using System.Collections.Generic;
using System.Linq;

// TODO: Since we are allowing certain fields to be set from outside the class,
//       as part of the flexibility efforts, some of those might need some additional
//       modifying, like.

public class TableArranger
{
    public class Cell
    {
        // The text of the cell.
        public string Text { get; set; }

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
            Text = contents;
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
    public List<List<Cell>> Contents { get; private set; }

    // These are just to customize how we want our table to look when we print it :)
    private string _rowSeparator;
    private string _colSeparator;
    private string _cornerMarker;

    public TableArranger()
    {
        NumColumns = 0;
        NumRows = 1;
        Contents = new List<List<Cell>>();

        _rowSeparator = "-";
        _colSeparator = "|";
        _cornerMarker = "+";

        // We know that a table by definition will have headers, so we add the
        // placeholder from here.
        Contents.Add(new List<Cell>());
    }

    public TableArranger(string[] entries,
                         string[] headers,
                         int[] lengths,
                         string rawDelimiter = " ") : this()
    {
        CreateTableFromData(entries, headers, lengths, rawDelimiter);
    }

    public void AddColumn(string colText = "<Column>", int maxLen = -1)
    {
        // The top row contains the headers, so we add it the cell with the name
        // of the column there.
        Contents[0].Add(new Cell(colText, maxLen));
        NumColumns++;

        // If we're adding a column after we already have data, we have to update
        // each row with the new cell.
        for (int i = 1; i < Contents.Count; i++)
        {
            Contents[i].Add(new Cell());
        }
    }

    public void AddRow(string[] values, List<int> lengths)
    {
        List<Cell> row = new List<Cell>(values.Length);

        for (int i = 0; i < values.Length; i++)
        {
            row.Add(new Cell(values[i], lengths[i]));
        }

        Contents.Add(row);
        NumRows++;
    }

    private void CreateTableFromData(string[] entries,
                                     string[] headers,
                                     int[] lengths,
                                     string rawDelimiter)
    {
        List<string> headersList = headers.ToList();
        List<int> lengthsList = lengths.ToList();
        CheckAndBalanceColumnsAndLengths(headersList, lengthsList);

        // First add all the headers to the first row of the table, which has
        // already been created by the default constructor called with "this()".

        for (int i = 0; i < headersList.Count; i++)
        {
            AddColumn(headersList[i], lengthsList[i]);
        }

        // This is the main core of creating a table from provided data. We read
        // each line, split it using the given raw delimiter, and then call AddRow(),
        // who takes care of creating the Cell objects and storing them into
        // the table.

        foreach (string rowData in entries)
        {
            string[] cellsData = rowData.Split(rawDelimiter);

            // Ensure the given delimiter split the data appropriately. I already
            // had an issue that took a bit to discover and fix.
            if (cellsData.Length != NumColumns)
            {
                throw new ArgumentOutOfRangeException(
                    $"Number of cells \"{cellsData.Length}\" in \"{rowData}\" does not"
                    + $" match the table's number of columns \"{NumColumns}\".");
            }

            AddRow(cellsData, lengthsList);
        }
    }

    private void CheckAndBalanceColumnsAndLengths(List<string> headers, List<int> lengths)
    {
        int numCols = headers.Count;
        int numColMaxLengths = lengths.Count;

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
            lengths.RemoveRange(lengths.Count - difference, difference);
        }
        else
        {
            Console.WriteLine($"There are {difference} less lengths than columns"
                              + " provided. The last {difference} columns will be"
                              + " set to have no character limit (i.e. -1).");
            lengths.AddRange(Enumerable.Repeat(-1, difference));
        }
    }

    public void DisplayTable(bool separateAllRows = true)
    {
        // First things first. If we have any column(s) without size limits, then
        // we need to figure it out by finding the longest string in it, and use
        // its length as limit.
        SetLengthsToUnlimitedColumns();

        // What we've all been expecting: Print the table!
        DisplayHeaders();
        for (int i = 1; i < NumRows - 1; i++)
        {
            DisplayRow(Contents[i], false);
        }

        DisplayRow(Contents[NumRows - 1], true);
        Console.Write("\n");
    }

    private void DisplayHeaders()
    {
        DisplayRowSeparator(true);
        DisplayRow(Contents[0], true);
    }

    private void DisplayRow(List<Cell> row, bool isHeaderOrFooter = false)
    {
        Console.Write(_colSeparator);
        int[] partialCells = new int[NumColumns];

        // Will implement the wrapping later. I need to finish the base functionality
        // today, and I already got more work.
        for (int i = 0; i < row.Count; i++)
        {
            Cell c = row[i];
            partialCells[i] = c.WrapsNeeded;

            // For all cells smaller than the longest text or set character limit,
            // we need to pad them, so they fit the cell size accordingly. And for
            // larger ones, we have to trim them to the limit set.

            string textToPrint = c.WrapsNeeded > 0 ? c.Text.Substring(0, c.MaxLength)
                                                   : c.Text;
            Console.Write($" {textToPrint.PadRight(c.MaxLength)} {_colSeparator}");
        }

        Console.Write("\n");
        int finalWraps = partialCells.Max();

        if (finalWraps <= 0)
        {
            DisplayRowSeparator(isHeaderOrFooter);
            return ;
        }

        // We need as many new "partial rows" as the maximum number of wraps
        // needed in this row. One for each wrap.
        for (int j = 0; j < finalWraps; j++)
        {
            Console.Write(_colSeparator);

            for (int k = 0; k < partialCells.Length; k++)
            {
                Cell c = row[k];

                // If this cell doesn't need any (more) wraps, then just fill
                // its content with whitespace.
                if (partialCells[k] == 0)
                {
                    // Adding a +2 here to account for the leading and trailing
                    // blank spaces.
                    Console.Write(new String(' ', c.MaxLength + 2)
                                  + _colSeparator);
                    continue;
                }

                // If there's yet another wrap pending, display the next segment.
                // Otherwise, display the rest of the string. We have to make this
                // distinction because if the next segment is shorter, then we would
                // get an ArgumentOutOfRangeException.
                string nextWrapText = c.Text.Substring(
                    c.MaxLength * (j+1),
                    Math.Min(c.MaxLength * (j+2), c.Text.Length - 1));

                Console.Write($" {nextWrapText.PadRight(c.MaxLength)} {_colSeparator}");

                // Decrement by one because this wrap has been taken care of.
                partialCells[k]--;
            }
            Console.Write("\n");
        }
        DisplayRowSeparator(isHeaderOrFooter);
    }

    private void DisplayRowSeparator(bool isHeaderOrFooter = false,
                                     bool thickEdges = false)
    {
        string delimiter = isHeaderOrFooter ? _cornerMarker : _colSeparator;
        int thickness = (isHeaderOrFooter & thickEdges) ? 2 : 1;

        // We want to emphasize the table's headers and footers by making their
        // borders twice as thick.

        for (int i = 0; i < thickness; i++)
        {
            Console.Write(delimiter);

            foreach (Cell c in Contents[0])
            {
                // We're adding +2 to the amount of characters because we're going to
                // pad each cell with a heading and a trailing space. Just to improve
                // the text's readability.

                Console.Write(String.Join("", Enumerable.Repeat(_rowSeparator,
                                                                c.MaxLength + 2)));
                Console.Write(delimiter);
            }

            Console.Write("\n");
        }
    }

    private void SetLengthsToUnlimitedColumns()
    {
        for (int i = 0; i < NumColumns; i++)
        {
            int columnSize = Contents[0][i].MaxLength;

            // If said column has a set size, then there's nothing else to prepare.
            if (columnSize > 0)
                continue;

            // Search that column in all the rows in the table to find the
            // longest text.
            int newColumnSize = Contents.Max(row => row[i].Text.Length);

            // Finally, we set the newly calculated length to our cell objects.
            for (int j = 0; j < NumRows; j++)
            {
                Contents[j][i].MaxLength = newColumnSize;
            }
        }
    }
}
