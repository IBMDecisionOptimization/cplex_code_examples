//   Copyright 2020 IBM Corporation
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using System;
using System.Collections.Generic;

using ILOG.OPL;
using ILOG.Concert;
using System.Collections;
using System.Data.Common;
using System.Data.SQLite;
using System.Data;

/// <summary>
///  A simple custom data source that illustrates how data can be loaded
///  from more or less arbitrary sources. The design is so that any data
///  that is layed out in tables (SQL, CSV, ...) can be loaded easily.
/// </summary>
public class CustomDataSource : CustomOplDataSource
{
    /// <summary>
    ///  The name of the element that provides the description of the
    ///  elements to be loaded.
    /// </summary>
    private static readonly string ELEM_NAME = "customLoad";
    /// <summary>
    ///  This is the name of the field in the <code>customLoad</code> tuple
    ///  that specifies the connection string.
    /// </summary>
    private static readonly string CONN_FIELD = "connection";
    /// <summary>
    ///  This is the name of the field in the <code>customLoad</code> tuple
    ///  that specifies the type of connection.
    /// </summary>
    private static readonly string TYPE_FIELD = "type";
    /// <summary>
    ///  This is the name of the field in the <code>customLoad</code> tuple
    ///  that specifies which elements to load and how to load them.
    /// </summary>
    private static readonly string DATA_FIELD = "data";

    /// <summary>
    ///  A row in a table of data.
    ///  An instance of this interface represents one row in the collection
    ///  that the IEnumerator<IRow> interface iterates over.
    /// </summary>
    interface IRow
    {
        /// <summary>
        ///  Get the value of an element as integer.
        /// </summary>
        /// <param name="idx">Index of element to query (0-based).</param>
        /// <returns>The element at position <code>idx</code> converted to <code>int</code></returns>
        public int GetIntColumn(int idx);
        /// <summary>
        ///  Get the value of an element as floating point number.
        /// </summary>
        /// <param name="idx">Index of element to query (0-based).</param>
        /// <returns>The element at position <code>idx</code> converted to <code>double</code></returns>
        public double GetFloatColumn(int idx);
        /// <summary>
        ///  Get the value of an element as string.
        /// </summary>
        /// <param name="idx">Index of element to query (0-based).</param>
        /// <returns>The element at position <code>idx</code> converted to <code>string</code></returns>
        public String GetStringColumn(int idx);
    }
    /// <summary>
    /// Interface that is implemented by all the data providers here.
    /// The <code>getRows()</code> function gives a row-oriented view on the data
    /// provided by this data source.
    /// </summary>
    interface IDataProvider : IDisposable
    {
        /// <summary>
        /// Get the rows specified by <code>stmt</code>
        /// </summary>
        /// <param name="stmt">A string that specifies which rows are requested from this source.</param>
        /// <returns>The rows identified by <code>stmt</code></returns>
        IEnumerator<IRow> getRows(string stmt);
    }

    public CustomDataSource(OplFactory factory) : base(factory) { }

    public override void CustomRead()
    {
        OplDataHandler handler = DataHandler;

        // Get the element of the predefined name.
        // If no such element is defined then exit early.
        // The element must be either a tuple or a set of tuples.
        // Any custom load specification specified by the element is then processed.
        OplElement spec = handler.getElement(ELEM_NAME);
        if (spec == null)
            return;
        if (spec.ElementType == OplElementType.Type.TUPLE)
        {
            loadSpec(handler, spec.AsTuple());
        }
        else if (spec.ElementType == OplElementType.Type.SET_TUPLE)
        {
            foreach (ITuple tuple in spec.AsTupleSet())
                loadSpec(handler, tuple);
        }
        else
            throw new NotImplementedException(ELEM_NAME + " must be (set of) tuple");
    }

    /// <summary>
    ///  Look for a string field in a tuple and return its value.
    /// </summary>
    /// <param name="tuple">The tuple to search.</param>
    /// <param name="field">The name of the field to find.</param>
    /// <param name="def">The value to return if the field is not found or is not of string type.</param>
    /// <returns></returns>
    private static string tryLoadField(ITuple tuple, String field, string def = null)
    {
        ITupleSchema schema = tuple.Schema;
        int size = schema.Size;
        for (int i = 0; i < size; ++i)
        {
            if (schema.IsSymbol(i) && field.Equals(schema.GetColumnName(i)))
                return tuple.GetStringValue(i);
        }
        return def;
    }

    /// <summary>
    ///  Process one load specification.
    /// </summary>
    /// <param name="handler">The data loader that constructs OPL elements.</param>
    /// <param name="tuple">The load specification.</param>
    private void loadSpec(OplDataHandler handler, ITuple tuple)
    {
        // Get the connection string
        // In the SimpleData implementation we don't use that string.
        // If you create a data source that is backed up by a file or
        // by a database, then this string can be used to specify
        // locations and/or credentials.
        string connection = tuple.GetStringValue(CONN_FIELD);

        string connectionType = tryLoadField(tuple, TYPE_FIELD, "SimpleData");
        IDataProvider provider = null;
        if (connectionType.Equals("SimpleData"))
            provider = new SimpleData(connection);
        else if (connectionType.Equals("SQLite"))
            provider = new SQLiteData(connection);
        else
            provider = new GenericData(connectionType, connection);

        // Process each of load specifications and load the respective
        // element.
        using (provider) {
            ISymbolSet data = tuple.GetSymbolSetValue(DATA_FIELD);
            IEnumerator e = data.GetEnumerator();
            while (e.MoveNext())
            {
                // Split specification into element name and
                // initialiation string (statement).
                string s = e.Current.ToString().Trim();
                int eq = s.IndexOf('=');
                string name = s.Substring(0, eq).Trim();
                string stmt = s.Substring(eq + 1).Trim();

                // Inspect the type of the element and populate it.
                OplElement elem = handler.getElement(name);
                OplElementType.Type type = elem.ElementType;
                using (IEnumerator<IRow> rows = provider.getRows(stmt))
                {

                    // (collections of) integers
                    if (type == OplElementType.Type.INT)
                        loadPrimitive(handler, name, rows, SET_INT);
                    else if (type == OplElementType.Type.SET_INT)
                        loadPrimitiveCollection(handler, name, rows, START_SET, END_SET, SET_INT);
                    else if (type == OplElementType.Type.MAP_INT)
                        loadPrimitiveCollection(handler, name, rows, START_ARRAY, END_ARRAY, SET_INT);

                    // (collections of) floating point values
                    else if (type == OplElementType.Type.NUM)
                        loadPrimitive(handler, name, rows, SET_FLOAT);
                    else if (type == OplElementType.Type.SET_NUM)
                        loadPrimitiveCollection(handler, name, rows, START_SET, END_SET, SET_FLOAT);
                    else if (type == OplElementType.Type.MAP_NUM)
                        loadPrimitiveCollection(handler, name, rows, START_ARRAY, END_ARRAY, SET_FLOAT);

                    // (collections of) tuples
                    else if (type == OplElementType.Type.STRING)
                        loadPrimitive(handler, name, rows, SET_STRING);
                    else if (type == OplElementType.Type.SET_SYMBOL)
                        loadPrimitiveCollection(handler, name, rows, START_SET, END_SET, SET_STRING);
                    else if (type == OplElementType.Type.MAP_SYMBOL)
                        loadPrimitiveCollection(handler, name, rows, START_ARRAY, END_ARRAY, SET_STRING);

                    else if (type == OplElementType.Type.TUPLE)
                        loadTuple(handler, name, rows);
                    else if (type == OplElementType.Type.SET_TUPLE)
                        loadTupleCollection(handler, name, rows, elem.AsTupleSet().Schema, START_SET, END_SET);
                    else if (type == OplElementType.Type.MAP_TUPLE)
                        loadTupleCollection(handler, name, rows, elem.AsTupleMap().Schema, START_ARRAY, END_ARRAY);
                    else
                        throw new NotImplementedException("element type " + type + " not implemented");
                }
            }
        }
    }

    /// <summary>
    ///  Load an element that is just a value.
    /// </summary>
    private void loadPrimitive(OplDataHandler handler, string name, IEnumerator<IRow> rows, Action<OplDataHandler, IRow, int> setValue)
    {
        handler.StartElement(name);
        rows.MoveNext();
        setValue(handler, rows.Current, 0);
        handler.EndElement();
    }

    /// <summary>
    ///  Recursively load data into the tuple current being loaded.
    /// </summary>
    /// <param name="handler">The handler constructing the current element.</param>
    /// <param name="schema">Schema of tuple being constructed.</param>
    /// <param name="row">Data source for tuple to construct.</param>
    /// <param name="absIndex">Absolute index into <code>row</code> for the next column to process.</param>
    /// <returns>The updated absolute index.</returns>
    private int fillTuple(OplDataHandler handler, ITupleSchema schema, IRow row, int absIndex)
    {
        handler.StartTuple();
        for (int i = 0; i < schema.Size; ++i)
        {
            if (schema.IsTuple(i))
            {
                ILOG.Opl_Core.Cppimpl.IloTupleSchema s = (ILOG.Opl_Core.Cppimpl.IloTupleSchema)schema;
                absIndex = fillTuple(handler, s.getTupleColumn(i), row, absIndex);
                continue;
            }
            if (schema.IsNum(i))
                SET_FLOAT(handler, row, absIndex);
            else if (schema.IsInt(i))
                SET_INT(handler, row, absIndex);
            else if (schema.IsSymbol(i))
                SET_STRING(handler, row, absIndex);
            else
                throw new NotImplementedException("tuple element type not supported");
            ++absIndex;
        }
        handler.EndTuple();
        return absIndex;
    }

    /// <summary>
    ///  Load an element that is a plain tuple.
    /// </summary>
    private void loadTuple(OplDataHandler handler, string name, IEnumerator<IRow> rows)
    {
        OplElement elem = handler.getElement(name);
        ITuple tuple = elem.AsTuple();
        handler.StartElement(name);
        rows.MoveNext();
        fillTuple(handler, tuple.Schema, rows.Current, 0);
        handler.EndElement();
    }

    /// <summary>
    /// Delegate to set an integer value in data.
    /// </summary>
    private static readonly Action<OplDataHandler, IRow, int> SET_INT = delegate (OplDataHandler handler, IRow row, int idx)
    {
        handler.AddIntItem(row.GetIntColumn(idx));
    };

    /// <summary>
    /// Delegate to set a floating value in data.
    /// </summary>
    private static readonly Action<OplDataHandler, IRow, int> SET_FLOAT = delegate (OplDataHandler handler, IRow row, int idx)
    {
        handler.AddNumItem(row.GetFloatColumn(idx));
    };
    /// <summary>
    /// Delegate to set a string value in data.
    /// </summary>
    private static readonly Action<OplDataHandler, IRow, int> SET_STRING = delegate (OplDataHandler handler, IRow row, int idx)
    {
        handler.AddStringItem(row.GetStringColumn(idx));
    };

    /// <summary>
    /// Delegate to start a collection of type SET.
    /// </summary>
    private static readonly Action<OplDataHandler> START_SET = h => h.StartSet();
    /// <summary>
    /// Delegate to end a collection of type SET.
    /// </summary>
    private static readonly Action<OplDataHandler> END_SET = h => h.EndSet();
    /// <summary>
    /// Delegate to start a collection of type ARRAY.
    /// </summary>
    private static readonly Action<OplDataHandler> START_ARRAY = h => h.StartArray();
    /// <summary>
    /// Delegate to end a collection of type ARRAY.
    /// </summary>
    private static readonly Action<OplDataHandler> END_ARRAY = h => h.EndArray();

    /// <summary>
    ///  Load a collection or array of primitive types.
    /// </summary>
    private void loadPrimitiveCollection(OplDataHandler handler, string name, IEnumerator<IRow> rows, Action<OplDataHandler> startCollection, Action<OplDataHandler> endCollection, Action<OplDataHandler,IRow,int> setValue)
    {
        handler.StartElement(name);
        startCollection(handler);
        while (rows.MoveNext())
        {
            setValue(handler, rows.Current, 0);
        }
        endCollection(handler);
        handler.EndElement();
    }

    /// <summary>
    ///  Load a collection of tuples types.
    /// </summary>
    private void loadTupleCollection(OplDataHandler handler, string name, IEnumerator<IRow> rows, ITupleSchema schema, Action<OplDataHandler> startCollection, Action<OplDataHandler> endCollection)
    {
        handler.StartElement(name);
        startCollection(handler);
        while (rows.MoveNext())
            fillTuple(handler, schema, rows.Current, 0);
        endCollection(handler);
        handler.EndElement();
    }

    /// <summary>
    ///  Simple implementation of IEnumerator<IRow> that is baced up by a string. 
    ///  To construct rows, the string is split at ';'. To construct columns each
    ///  row's string is split at ','.
    /// </summary>
    private class SimpleData : IDataProvider
    {
        private readonly char rowSeparator = ';';
        private readonly char columnSeparator = ',';

        public SimpleData(string unused)
        {
        }

        public IEnumerator<IRow> getRows(string stmt)
        {
            string[] rawRows = stmt.Split(rowSeparator);
            string[][] rows = new string[rawRows.Length][];
            for (int i = 0; i < rawRows.Length; ++i)
            {
                rows[i] = rawRows[i].Trim().Split(columnSeparator);
            }
            return new RowIterator(rows);
        }

        private class Row : IRow
        {
            private string[] data;
            public Row(string[] data) { this.data = data; }
            public double GetFloatColumn(int idx) => double.Parse(data[idx].Trim());
            public int GetIntColumn(int idx) => int.Parse(data[idx].Trim());
            public string GetStringColumn(int idx) => data[idx];

            override public string ToString() { return String.Join(',', data); }
        }

        private class RowIterator : IEnumerator<IRow>
        {
            private IRow current;
            private int next;
            private string[][] rows;
            public RowIterator(string[][] rows)
            {
                current = null;
                next = 0;
                this.rows = rows;
            }
            public IRow Current => current;

            object IEnumerator.Current => current;
            public bool MoveNext()
            {
                if (next < rows.Length)
                {
                    current = new Row(rows[next]);
                    ++next;
                    return true;
                }
                else
                {
                    current = null;
                    return false;
                }
            }

            public void Reset()
            {
                current = null;
                next = 0;
            }

            public void Dispose()
            {
                // Nothing to do here.
            }
        }
        public void Dispose()
        {
            // Nothing to do here.
        }
    }

    /// <summary>
    /// Data provider that is backed up by an SQLite database.
    /// </summary>
    private class SQLiteData : IDataProvider
    {
        private SQLiteConnection conn;

        public SQLiteData(string connstr)
        {
            conn = new SQLiteConnection(connstr);
            conn.Open();
        }

        public void Dispose()
        {
            if (conn != null)
            {
                conn.Close();
                conn.Dispose();
                conn = null;
            }
        }

        private class Rows : IEnumerator<IRow>
        {
            private Row current;
            private SQLiteCommand cmd;
            private SQLiteDataReader reader;
            public Rows(SQLiteConnection conn, string stmt)
            {
                current = null;
                cmd = new SQLiteCommand(stmt, conn);
                reader = cmd.ExecuteReader();
            }
            public IRow Current => current;

            object IEnumerator.Current => current;

            public void Dispose()
            {
                if (reader != null)
                {
                    reader.DisposeAsync();
                    reader = null;
                }

                if (cmd != null)
                {
                    cmd.Dispose();
                    cmd = null;
                }
            }

            private class Row : IRow
            {
                private readonly SQLiteDataReader reader;
                public Row(SQLiteDataReader reader)
                {
                    this.reader = reader;
                }
                public double GetFloatColumn(int idx) => reader.GetDouble(idx);
                public int GetIntColumn(int idx) => reader.GetInt32(idx);
                public string GetStringColumn(int idx) => reader.GetString(idx);
            }

            public bool MoveNext()
            {
                if (reader.Read())
                {
                    current = new Row(reader);
                    return true;
                }
                else
                {
                    current = null;
                    return false;
                }
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }

        public IEnumerator<IRow> getRows(string stmt)
        {
            return new Rows(conn, stmt);
        }
    }

    /// <summary>
    /// A generic data source.
    /// This class uses the <code>connType</code> argument to the constructor to dynamically
    /// figure out the database provider. It should work for any database provider that is
    /// properly registered with .NET.
    /// </summary>
    private class GenericData : IDataProvider
    {
        private DbConnection conn;

        /// <summary>
        /// Create a new generic data source.
        /// </summary>
        /// <param name="conntype">The connection type to create. This string is passed to
        /// <code>DbProviderFactories.getFactory()</code>code> to load the required
        /// connection factory.</param>
        /// <param name="connstr">Database connection string. This is used to create an actual
        /// connection from the connection factory.</param>
        public GenericData(string conntype, string connstr)
        {
            // In order to have the code self-contained, we explicitly register
            // the SQLite provider factory here. In production code this registration
            // should of course be done in some global system configuration.
            string sqlite = "System.Data.SQLite";
            try
            {
                DbProviderFactories.GetFactory(sqlite);
            }
            catch (SystemException)
            {
                DbProviderFactories.RegisterFactory(sqlite, new SQLiteFactory());
            }

            // Find a provider factory for conntype.
            System.Data.DataTable allFactories = DbProviderFactories.GetFactoryClasses();
            DbProviderFactory factory = null;
            foreach (DataRow r in allFactories.Rows)
            {
                // Potentially check whether this is the correct provider for
                // this connection type
                if (false)
                {
                    factory = DbProviderFactories.GetFactory(r);
                    break;
                }
            }
                
            if (factory == null)
                factory = DbProviderFactories.GetFactory(conntype);

            // Create and open connection from factory.
            conn = factory.CreateConnection();
            conn.ConnectionString = connstr;
            conn.Open();
        }

        public void Dispose()
        {
            if (conn != null)
            {
                conn.Close();
                conn.Dispose();
                conn = null;
            }
        }

        private class Rows : IEnumerator<IRow>
        {
            private Row current;
            private DbCommand cmd;
            private DbDataReader reader;
            public Rows(DbConnection conn, string stmt)
            {
                current = null;
                cmd = conn.CreateCommand();
                cmd.CommandText = stmt;
                reader = cmd.ExecuteReader();
            }
            public IRow Current => current;

            object IEnumerator.Current => current;

            public void Dispose()
            {
                if (reader != null)
                {
                    reader.DisposeAsync();
                    reader = null;
                }

                if (cmd != null)
                {
                    cmd.Dispose();
                    cmd = null;
                }
            }

            private class Row : IRow
            {
                private readonly DbDataReader reader;
                public Row(DbDataReader reader)
                {
                    this.reader = reader;
                }
                public double GetFloatColumn(int idx) => reader.GetDouble(idx);
                public int GetIntColumn(int idx) => reader.GetInt32(idx);
                public string GetStringColumn(int idx) => reader.GetString(idx);
            }

            public bool MoveNext()
            {
                if (reader.Read())
                {
                    current = new Row(reader);
                    return true;
                }
                else
                {
                    current = null;
                    return false;
                }
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }

        public IEnumerator<IRow> getRows(string stmt)
        {
            return new Rows(conn, stmt);
        }
    }
}
