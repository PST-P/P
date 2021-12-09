package psymbolic.valuesummary.util;

import psymbolic.valuesummary.Guard;
import psymbolic.valuesummary.PrimitiveVS;

import java.util.*;

public class ValueSummaryUnionFind<T> extends UnionFind<PrimitiveVS<T>> {

    Map<PrimitiveVS<T>, Guard> universe = new HashMap<>();

    public ValueSummaryUnionFind(Collection<PrimitiveVS<T>> c) {
        super();
        for (PrimitiveVS<T> elt : c) {
            List<PrimitiveVS<T>> values = new ArrayList<>(new HashSet<>(parents.values()));
            addElement(elt);
            Guard eltUniverse = elt.getUniverse();
            for (int i = 0; i < values.size(); i ++) {
                Guard unionUniverse = universe.get(find(values.get(i)));
                if (!eltUniverse.and(unionUniverse).isFalse()) {
                    union(elt, values.get(i));
                    if (eltUniverse.implies(unionUniverse).isTrue()) {
                        break;
                    }
                }
            }
        }
    }

    public Map<Set<PrimitiveVS<T>>, Guard> getLastUniverseMap() {
        Map<Set<PrimitiveVS<T>>, Guard> lastUniverseMap = new HashMap<>();
        for (Set<PrimitiveVS<T>> set : lastDisjointSet) {
            lastUniverseMap.put(set, universe.get(find(set.iterator().next())));
        }
        return lastUniverseMap;
    }

    public void addElement(PrimitiveVS<T> elt) {
        super.addElement(elt);
        universe.put(elt, elt.getUniverse());
    }

    public boolean union(PrimitiveVS<T> e1, PrimitiveVS<T> e2) {
        Guard universe1 = universe.get(find(e1));
        Guard universe2 = universe.get(find(e2));
        boolean res = super.union(e1, e2);
        if (!res) return false;
        universe.put(find(e1), universe1.or(universe2));
        return res;
    }
}