// C++ implementation of the example described here:
// https://www.linkedin.com/pulse/what-optimization-how-can-help-you-do-more-less-zoo-buses-fleischer/

#include <iostream>
#include <ilcplex/ilocplex.h>

using std::cout;
using std::cerr;
using std::endl;

int
main(void)
{
   IloEnv env;
   try {
      IloModel model(env);

      // int nbKids = 300
      int nbKids = 300;
      // float costBus40 = 500
      // float costBus30 = 400
      double costBus40 = 500;
      double costBus30 = 400;

      // dvar int+ nbBus40;
      // dvar int+ nbBus30;
      IloIntVar nbBus40(env, 0, IloInfinity, "nbBus40");
      IloIntVar nbBus30(env, 0, IloInfinity, "nbBus30");

      // minimize costBus40 * nbBus40 + costBus30 * nbBus30
      IloExpr cost = costBus40 * nbBus40 + costBus30 * nbBus30;
      model.add(IloMinimize(env, cost));

      // subjec to {
      //  40 * nbBus40 + 30 * nbBus30 >= nbKids
      // }
      model.add(40 * nbBus40 + 30 * nbBus30 >= nbKids);

      IloCplex cplex(model);
      cplex.solve();

      cout << "Use " << cplex.getValue(nbBus40) << " buses of type 40" << endl;
      cout << "Use " << cplex.getValue(nbBus30) << " buses of type 30" << endl;
      cout << "Total cost: " << cplex.getValue(cost) << endl;

      env.end();
   }
   catch (IloException &e) {
      cerr << "exception: " << e << endl;
      throw;
   }
   return 0;
}
