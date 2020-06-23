// Untested code snippet. NO WARRANTY!
import ilog.cplex.IloCplex;
import ilog.concert.IloException;
import ilog.concert.IloLPMatrix;
import ilog.concert.IloMPModeler;
import ilog.concert.IloNumVar;
import ilog.concert.IloRange;
import java.util.Collection;
import java.util.Vector;

/** Example class that illustrates how to have multiple separators with
 * a single callback in CPLEX.
 */
public final class MultipleSeparators {

   /** Interface that represents a solution.
    * This could be the current solution at a callback as well as the solution
    * returned after solve().
    */
   public interface Solution {
      /** Get the value of a variable.
       * @param var The variable to query.
       * @return The current value for <code>var</code>.
       */
      public double getVariableValue(IloNumVar var) throws IloException;
   }

   /** Interface for separators.
    * Separators implement this interface in order to be invoked from the
    * callback's <code>main()</code> method.
    */
   public interface Separator {
      /** Separate constraints for the current solution represented by
       * <code>sol</code>.
       * @param sol The current solution.
       * @return A (potentially empty) list of violated constraints.
       */
      public Collection<IloRange> separate(Solution sol) throws IloException;
   }

   /** The callback that wraps the multiple separators. */
   public static class Callback
      extends IloCplex.LazyConstraintCallback
      implements Solution
   {

      /** The separators that are invoked from the callback. */
      private Collection<Separator> separators = new Vector<Separator>();

      public Callback(Collection<Separator> separators) {
         this.separators.addAll(separators);
      }

      @Override
      public double getVariableValue(IloNumVar var) throws IloException {
         return getValue(var);
      }

      public void main() throws IloException {
         // Go through all separators
         for (Separator s : separators)
            // and add all violated constraints found
            for (IloRange r : s.separate(this))
               add(r);
      }
   }

   /** Example separator. */
   private static final class Example implements Separator {
      /** The factory that is used to create violated constraints. */
      private final IloMPModeler factory;
      /** Variables for the separation algorithm. */
      private final IloNumVar[] x;
      public Example(IloMPModeler factory, IloNumVar[] x) {
         this.factory = factory;
         this.x = x;
      }

      public Collection<IloRange> separate(Solution sol) throws IloException {
         // Require any variable to have a value <= 100
         Collection<IloRange> violated = new Vector<IloRange>();
         for (int i = 0; i < x.length; ++i) {
            if ( sol.getVariableValue(x[i]) > 100 )
               violated.add(factory.le(x[i], 100));
         }
         System.out.println(violated.size() + " violated constraints found");
         return violated;
      }
   }

   public static void main(String[] args) throws IloException {
      for (String model : args) {
         IloCplex cplex = new IloCplex();
         try {
            cplex.importModel(model);
            Vector<Separator> separators = new Vector<Separator>();
            separators.add(new Example(cplex,
                                       ((IloLPMatrix)cplex.LPMatrixIterator().next()).getNumVars()));
            Callback cb = new Callback(separators);
            cplex.use(cb);
            cplex.solve();
         }
         finally {
            cplex.end();
         }
      }
   }
}
