# A custom data source for .NET applications

```
   Copyright 2020 IBM Corporation

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
```

The code provided here is intended to be used in .NET applications that use
OPL but need some sort of custom data input, for example from data bases.
In order to use the class, you have to register it with an OplModel instance.
For example, you can take the `OplRunSample.cs` example that is shipped with
CPLEX and replace
```
OplModel opl = rc.OplModel;
```
by
```
OplModel opl = rc.OplModel;
opl.AddDataSource(new CustomDataSource(oplF));
```

The class implements a simple data source that initializes data from text
and a data source that is backed up by an sqlite database.
The latter requires `System.Data.SQLite` which you may have to get from NuGet.
The sqlite data source can also serve as an example for connection other
databases or for creating a generic data source using ADO.NET drivers.

The data source works as follows:

1. It expects that the `.mod` file defines an element called `customLoad
   This element must be a tuple or a set of tuple. The tuple must have the
   following fields:
   - `connection` is the connection string used for connection to a database,
     a file on disk, ... This field must be a string. It is passed to the
     constructor of the implementation class.
   - `type` is the type of connection requested and decides which
     implementation is actually used. This must be a string. If the field
     does not exist then "SimpleData" is assumed.
   - `data` is a set of strings.
2. For each tuple specified in the previous step, the respective implementation
   class is instantiated with the corresponding connection string. Then for
   each string in the `data` field the following actions are performed:
   - the string is split at the *first* occurence of `=` into a name and value.
   - the name is considered to be the name of an element to be loaded. That
     element and its type are looked up.
   - the respective element is filled using the value. That value is specific
     to the data source type. For SimpleData it is just a literal that defines
     the value. For SQLite it is an SQL statement that fetches the required
     data from the database. For other implementations it could mean something
     else.

Note that for ease of exposition, the code does only very limited error
checking. You may want to make it more robust for production use.