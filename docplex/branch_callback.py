# --------------------------------------------------------------------------
# Source file provided under Apache License, Version 2.0, January 2004,
# http://www.apache.org/licenses/
# (c) Copyright IBM Corp. 2015, 2020
# --------------------------------------------------------------------------

# gendoc: ignore


# This file shows how to connect CPLEX branch callbacks to a DOcplex model.
import math
import cplex
import cplex.callbacks as cpx_cb

from docplex.mp.callbacks.cb_mixin import *

from collections import defaultdict

class MyBranch(ModelCallbackMixin, cpx_cb.BranchCallback):
    '''Branch callback.

    This inherits from the docplex mixin class as well as the CPLEX Python
    API branch callback class.
    '''
    brtype_map = {'0': 'var', '1': 'sos1', '2':'sos2', 'X': 'user'}
    def __init__(self, env):
        # non public...
        cpx_cb.BranchCallback.__init__(self, env)
        ModelCallbackMixin.__init__(self)
        self.nb_called = 0
        self.stats = defaultdict(int)

    def __call__(self):
        self.nb_called += 1

        # Get the type of branch that CPLEX has in mind for this node.
        # If CPLEX plans to do an SOS branch then accept this decision.
        # Otherwise branch on the most fractional variable.
        br_type = self.get_branch_type()
        if (br_type == self.branch_type.SOS1 or
                br_type == self.branch_type.SOS2):
            return

        # Get solution at this node. In order to get the docplex variable
        # object for a variable index you can use self.index_to_var, see below.
        x = self.get_values()

        objval = self.get_objective_value()
        obj = self.get_objective_coefficients()
        feas = self.get_feasibilities()

        maxobj = -cplex.infinity
        maxinf = -cplex.infinity
        bestj = -1
        infeas = self.feasibility_status.infeasible

        # Find the most fractional variable
        for j in range(len(x)):
            if feas[j] == infeas:
                xj_inf = x[j] - math.floor(x[j])
                if xj_inf > 0.5:
                    xj_inf = 1.0 - xj_inf

                if (xj_inf >= maxinf and
                        (xj_inf > maxinf or abs(obj[j]) >= maxobj)):
                    bestj = j
                    maxinf = xj_inf
                    maxobj = abs(obj[j])

        if bestj < 0:
            return

        xj_lo = math.floor(x[bestj])
        dv = self.index_to_var(bestj)
        self.stats[dv] += 1
        # note that we convert the variable index to its docplex name
        print('---> BRANCH[{0}]---  custom branch callback, branch type is {1}, var={2!s}'
              .format(self.nb_called, self.brtype_map.get(br_type, '??'), dv))

        # Create two new child nodes.
        # Note: the node_data argument can be any Python object or None.
        #       the value passed here is associated with the newly created
        #       node and can later be queried when the node is further
        #       processed. Here we store the branching decisions made.
        self.make_branch(objval, variables=[(bestj, "L", xj_lo + 1)],
                         node_data=(bestj, xj_lo, "UP"))
        self.make_branch(objval, variables=[(bestj, "U", xj_lo)],
                         node_data=(bestj, xj_lo, "DOWN"))

    def report(self, n=5):
        sorted_stats = sorted(self.stats.items(), key=lambda p: p[1], reverse=True)
        for k, (dv, occ) in enumerate(sorted_stats[:n], start=1):
            print('#{0} most branched: {1}, branched: {2}'.format(k, dv, occ))


def add_branch_callback(docplex_model, logged=False):
    # register a class callback once!!!
    bcb = docplex_model.register_callback(MyBranch)

    docplex_model.parameters.mip.interval = 1
    docplex_model.parameters.preprocessing.linear = 0

    solution = docplex_model.solve(log_output=logged)
    assert solution is not None
    docplex_model.report()

    bcb.report(n=3)


if __name__ == "__main__":
    # from examples.modeling.love_hearts import build_hearts
    # love10 = build_hearts(r=8)
    # add_branch_callback(love10)
    from examples.modeling.logical.lifegame import build_lifegame_model
    life_m = build_lifegame_model(n=6)
    add_branch_callback(life_m, logged=False)


