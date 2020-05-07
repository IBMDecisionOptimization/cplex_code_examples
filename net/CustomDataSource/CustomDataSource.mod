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

// A very simple model to illustrate custom loading.
// The most important thing here is to define a tuple
// "CustomLoad" (the name of the tuple type is arbitrary)
// and a value of that tuple that is called "customLoad"
// (that name is not arbitrary since the .NET code will look
// for an element of that name).
// The tuple type must have (at least) a string field called
// "connection" and a string set field called "data".
// The "connection" field is used to specifiy any connection
// strings or other stuff that is required to initialize
// reading of custom data.
// Each element in "data" is a name/value pair. The name is
// the name to be initialized, the value is the value to
// be used for initialization.
// See CustomDataSource.dat for an example.
tuple CustomLoad {
  string connection;
  string type;
  {string} data;
}
{CustomLoad} customLoad = ...;

int intValue = ...;
float floatValue = ...;
{int} intSet = ...;
{float} floatSet = ...;
{string} stringSet = ...;

tuple T {
  int i;
  string s;
  float f;
}
T tupleValue = ...;
{T} tupleSet = ...;
T tupleArray[1..2] = ...;
{T} sqliteTupleSet = ...;
{T} genericTupleSet = ...;

dvar boolean xIntSet[intSet];
dvar boolean xFloatSet[floatSet];
dvar boolean xStringSet[stringSet];
dvar boolean xTupleSet[tupleSet];

minimize
  sum(i in intSet) xIntSet[i] -
  sum(i in floatSet) xFloatSet[i] +
  sum(i in stringSet) xStringSet[i] -
  sum(i in tupleSet) xTupleSet[i];

subject to {}

execute {
  writeln("Hello CustomDataSource");
  writeln("intValue = " + thisOplModel.intValue);
  writeln("floatValue = " + thisOplModel.floatValue);

  writeln("intSet = " + thisOplModel.intSet);
  writeln("floatSet = " + thisOplModel.floatSet);
  writeln("stringSet = " + thisOplModel.stringSet);

  writeln("tupleValue = " + thisOplModel.tupleValue);
  writeln("tupleSet = " + thisOplModel.tupleSet);
  writeln("tupleArray = " + thisOplModel.tupleArray);

  writeln("sqliteTupleSet = " + sqliteTupleSet);
  writeln("genericTupleSet = " + genericTupleSet);
}
