# ---------------------------------------------------------------------------
# File: tsp.py
# ---------------------------------------------------------------------------
# Licensed Materials - Property of IBM
# 5725-A06 5725-A29 5724-Y48 5724-Y49 5724-Y54 5724-Y55 5655-Y21
# Copyright IBM Corporation 2009, 2017. All Rights Reserved.
#
# US Government Users Restricted Rights - Use, duplication or
# disclosure restricted by GSA ADP Schedule Contract with
# IBM Corp.
# ---------------------------------------------------------------------------
#
# This is the translation of the shipped COS example
#    opl/examples/opl/models/TravelingSalesmanProblem
# to docplex. It uses a lazy constraint callback to separate subtours.
# The code has been tested only with the gr17.dat input file that is in this
# shipped example.

import sys

from cplex.callbacks import LazyConstraintCallback

from docplex.mp.callbacks.cb_mixin import *
from docplex.mp.model import Model

def neighbors(node, sol, x, Edges):
    """Get the neighbors of NODE in the current tour in SOL."""
    return \
        [e[1] for e in Edges if e[0] == node and sol.get_value(x[e]) > 0.5] + \
        [e[0] for e in Edges if e[1] == node and sol.get_value(x[e]) > 0.5]

# Lazy constraint callback to separate subtour elimination constraints.
class DOLazyCallback(ConstraintCallbackMixin, LazyConstraintCallback):
    def __init__(self, env):
        LazyConstraintCallback.__init__(self, env)
        ConstraintCallbackMixin.__init__(self)

    def __call__(self):
        # Fetch variable values into a solution object
        sol = self.make_solution_from_vars(self.x.values())
        visited = set()
        for i in self.Cities:
            if i in visited: continue
            # Find the (sub)tour that includes city i
            start = i
            node = i
            subtour = [-1] * n
            size = 0
            # Loop until we get back to start
            nodes = list()
            while node != start or size == 0:
                visited.add(node)
                nodes.append(node)
                # Pick the neighbor that we did not yet visit on this (sub)tour
                succ = None
                for j in neighbors(node, sol, self.x, self.Edges):
                    if j == start or j not in visited:
                        succ = j
                        break
                # Move to the next neigbor
                subtour[node] = succ
                node = succ
                size += 1
            # If the tour does not touch every node then it is a subtour and
            # needs to be eliminated
            if size < self.n:
                print('Violated subtour of length %d (%d) found: %s' %
                      (size, n, ' - '.join([str(j) for j in nodes])))
                # Create a constraint that states that from the variables in
                # the subtour not all can be 1.
                tour = 0
                for j, k in enumerate(subtour):
                    if k >= 0:
                        tour += self.x[(min(j, k), max(j,k))]
                ct = tour <= size - 1
                unsats = self.get_cpx_unsatisfied_cts([ct], sol, tolerance=1e-6)
                for ct, cpx_lhs, sense, cpx_rhs in unsats:
                    print('Add violated subtour')
                    self.add(cpx_lhs, sense, cpx_rhs)
                # Stop separation, we separate only one subtour at a time.
                break


# Get the problem data.
# NOTE: The parser is very simple and has only been tested with the gr17.dat
#       input data from the TravelingSalesmanProblem shipped with COS.
n = None
Cities = None
Edges = None
dist = None
with open(sys.argv[1], 'r') as f:
    for line in f:
        if line.startswith('n = '):
            n = int(line[4:len(line) - 2])
            Cities = range(n)
            Edges = [(i, j) for i in Cities for j in Cities if i < j]
            print('n = %d' % n)
        elif line.startswith('dist = ['):
            dist = {}
            for e in Edges:
                for line in f:
                    dist[e] = int(line)
                    break

# Set up the TSP model
with Model(name = 'tsp') as m:
    x = m.binary_var_dict(Edges)
    m.minimize(m.sum(dist[e] * x[e] for e in Edges))

    # Each city is linked with two other cities
    for j in Cities:
        m.add_constraint(sum(x[e] for e in Edges if e[0] == j) +
                         sum(x[e] for e in Edges if e[1] == j) == 2)

    # Register a lazy constraint callback
    cb = m.register_callback(DOLazyCallback)
    # Store references to variables in callback instance so that we can use
    # it for separation
    cb.n = n
    cb.Edges = Edges
    cb.Cities = Cities
    cb.x = x
    m.lazy_callback = cb

    # Solve the model.
    m.solve(log_output = True)

    sol = m.solution
    print('Optimal tour has length %f' % sol.get_objective_value())
    tour = list()
    start = Cities[0]
    node = start
    visited = set()
    while len(tour) == 0 or node is not start:
        tour.append(node)
        visited.add(node)
        for j in neighbors(node, sol, x, Edges):
            if j == start or j not in visited:
                neighbor = j
                break
        node = neighbor
    print('Optimal tour: %s' % ' - '.join([str(j) for j in tour]))
    assert len(tour) == n

