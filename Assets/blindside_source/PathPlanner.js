/* ---------- PathPlanner ----------
 * Standalone port of the dijkstra/planPath/truncate logic from
 * 01_core_data_map_vision.js, decoupled from team/unit state.
 * Input: a mouse-drag coordinate sequence (array of {x,y} in grid space).
 * Output: a wall-avoiding shortest path from the drag's start to its end,
 * simplified into straight segments.
 */
class PathPlanner {
  /**
   * @param {number} width  grid width
   * @param {number} height grid height
   * @param {(x:number,y:number)=>boolean} isBlocked  true if cell (x,y) is impassable
   * @param {(x:number,y:number)=>boolean} [isFree]   true if a point (continuous coords) has clearance; defaults to !isBlocked on the rounded cell
   */
  constructor(width, height, isBlocked, isFree) {
    this.W = width;
    this.H = height;
    this.isBlocked = isBlocked;
    this.isFree = isFree || ((px, py) => !isBlocked(Math.round(px), Math.round(py)));
    this.DIR8 = [[1,0],[-1,0],[0,1],[0,-1],[1,1],[1,-1],[-1,1],[-1,-1]];
  }

  idx(x, y) { return y * this.W + x; }
  dist(a, b) { return Math.hypot(a.x - b.x, a.y - b.y); }

  inBounds(x, y) { return x >= 0 && y >= 0 && x < this.W && y < this.H; }

  pass(x, y) { return this.inBounds(x, y) && !this.isBlocked(x, y); }

  segFree(a, b) {
    const d = Math.hypot(b.x - a.x, b.y - a.y);
    const n = Math.max(1, Math.ceil(d / 0.08));
    for (let i = 1; i <= n; i++) {
      const t = i / n;
      if (!this.isFree(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t)) return false;
    }
    return true;
  }

  /* 8-direction dijkstra over the grid, corner-cutting disallowed. */
  dijkstra(sx, sy) {
    const N = this.W * this.H;
    const D = new Float32Array(N).fill(1e9);
    const P = new Int32Array(N).fill(-1);
    const done = new Uint8Array(N);
    const s = this.idx(sx, sy);
    D[s] = 0;
    const open = [s];
    while (open.length) {
      let bi = 0;
      for (let i = 1; i < open.length; i++) if (D[open[i]] < D[open[bi]]) bi = i;
      const c = open[bi];
      open[bi] = open[open.length - 1];
      open.pop();
      if (done[c]) continue;
      done[c] = 1;
      const cx = c % this.W, cy = (c / this.W) | 0;
      for (const [dx, dy] of this.DIR8) {
        const nx = cx + dx, ny = cy + dy;
        if (!this.pass(nx, ny)) continue;
        const n = this.idx(nx, ny);
        if (done[n]) continue;
        if (dx && dy && (!this.pass(cx + dx, cy) || !this.pass(cx, cy + dy))) continue;
        const nd = D[c] + (dx && dy ? 1.4142 : 1);
        if (nd < D[n]) { D[n] = nd; P[n] = c; open.push(n); }
      }
    }
    return { D, P };
  }

  /* Collapse a point list into the fewest straight segments with clear line of sight. */
  simplify(pts) {
    const out = [pts[0]];
    let i = 0;
    while (i < pts.length - 1) {
      let j = pts.length - 1;
      while (j > i + 1 && !this.segFree(pts[i], pts[j])) j--;
      out.push(pts[j]);
      i = j;
    }
    return out;
  }

  /* Trim a point list to at most length b, starting from pts[0]. */
  truncate(pts, b) {
    const out = [pts[0]];
    let rem = b;
    for (let i = 1; i < pts.length; i++) {
      const s = this.dist(pts[i], pts[i - 1]);
      if (s <= rem) { out.push(pts[i]); rem -= s; }
      else {
        const t = rem / s;
        if (rem > 0.02) out.push({ x: pts[i - 1].x + (pts[i].x - pts[i - 1].x) * t, y: pts[i - 1].y + (pts[i].y - pts[i - 1].y) * t });
        break;
      }
    }
    return out;
  }

  /**
   * Plan a wall-avoiding shortest path from a mouse-drag coordinate sequence.
   * Uses only the drag's start and end points as endpoints; the interior
   * drag samples are ignored (they express intent, not the actual route).
   * @param {{x:number,y:number}[]} dragPoints
   * @param {number} [budget] optional max path length to truncate to
   * @returns {{x:number,y:number}[]|null} simplified path, or null if unreachable
   */
  planPath(dragPoints, budget) {
    if (!dragPoints || dragPoints.length === 0) return null;
    const from = dragPoints[0];
    const to = dragPoints[dragPoints.length - 1];
    const sx = Math.round(from.x), sy = Math.round(from.y);
    const gx = Math.round(to.x), gy = Math.round(to.y);
    if (!this.inBounds(gx, gy) || this.isBlocked(gx, gy)) return null;

    const { D, P } = this.dijkstra(sx, sy);
    const gk = this.idx(gx, gy);
    if (D[gk] >= 1e9) return null;

    const cells = [];
    let c = gk;
    const sk = this.idx(sx, sy);
    while (c !== sk && c >= 0) { cells.push({ x: c % this.W, y: (c / this.W) | 0 }); c = P[c]; }
    cells.reverse();

    const pts = [{ x: from.x, y: from.y }, ...cells];
    const end = { x: to.x, y: to.y };
    const last = pts[pts.length - 1];
    if (this.isFree(to.x, to.y) && this.segFree(last, end) && this.dist(last, end) > 0.05) {
      if (cells.length && this.segFree(pts[pts.length - 2], end)) pts[pts.length - 1] = end;
      else pts.push(end);
    }

    const out = this.simplify(pts);
    return budget != null ? this.truncate(out, budget) : out;
  }
}

if (typeof module !== 'undefined' && module.exports) module.exports = PathPlanner;
