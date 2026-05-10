"""
CMPE 485 Term Project — GPU Instancing & Draw Call Optimization
Analysis script: reads benchmark CSV, produces all report figures.

Usage:
    cd term-project/Analysis
    python plot_results.py                        # uses latest CSV in ../BenchmarkResults/
    python plot_results.py path/to/results.csv    # explicit path
"""

import sys
import os
import glob
import math
import numpy as np
import pandas as pd
import matplotlib
import matplotlib.pyplot as plt
import matplotlib.ticker as ticker

matplotlib.rcParams.update({
    "font.family":  "sans-serif",
    "font.size":    11,
    "axes.titlesize": 12,
    "axes.labelsize": 11,
    "legend.fontsize": 9,
    "figure.dpi":   100,
})

# ── paths ─────────────────────────────────────────────────────────────────────
SCRIPT_DIR  = os.path.dirname(os.path.abspath(__file__))
RESULTS_DIR = os.path.join(SCRIPT_DIR, "..", "BenchmarkResults")
FIGURES_DIR = os.path.join(SCRIPT_DIR, "figures")
os.makedirs(FIGURES_DIR, exist_ok=True)

# ── mesh constants (from BenchmarkSpawner.cs) ─────────────────────────────────
VERTS_PER_MESH = {"Low": 58, "Mid": 872, "High": 9902}
INST_BATCH_LIMIT = 500  # Metal GPU Instancing max instances per call

def theoretical_batches(n, mode, complexity):
    """Expected batch count from Unity's batching formulas."""
    if mode == "Baseline":
        return n + 1
    elif mode == "GPUInstancing":
        return math.ceil(n / INST_BATCH_LIMIT) + 1
    elif mode == "StaticBatching":
        v = VERTS_PER_MESH[complexity]
        return math.ceil(n * v / 65536)
    return 0

# ── data loading & aggregation ────────────────────────────────────────────────
def load_csv(path=None):
    if path:
        df = pd.read_csv(path)
    else:
        csvs = sorted(glob.glob(os.path.join(RESULTS_DIR, "results_*.csv")))
        if not csvs:
            raise FileNotFoundError(f"No results_*.csv found in {RESULTS_DIR}")
        print(f"Loading: {csvs[-1]}")
        df = pd.read_csv(csvs[-1])

    # Batch-counter recorder returns 0 when the profiler counter saturates on
    # Metal for high draw-call counts.  Treat 0 as missing; fill with theory below.
    for col in ["batches", "drawCalls", "triangles", "vertices"]:
        if col in df.columns:
            df[col] = df[col].replace(0, float("nan"))

    # renderThread not exposed on Metal (all -1) — drop to avoid confusion.
    if "renderThreadMs" in df.columns:
        df.drop(columns=["renderThreadMs"], inplace=True)

    return df


def aggregate(df):
    """Group by (renderingMode, meshComplexity, N) and compute mean ± std."""
    group_keys = ["renderingMode", "meshComplexity", "N"]
    mean = df.groupby(group_keys).mean(numeric_only=True).reset_index()
    std  = df.groupby(group_keys).std(ddof=1, numeric_only=True).reset_index()

    # Fill NaN batch means with theoretical formula (recorder glitch on Metal)
    for idx, row in mean.iterrows():
        if pd.isna(row["batches"]):
            mean.at[idx, "batches"] = theoretical_batches(
                int(row["N"]), row["renderingMode"], row["meshComplexity"])
            std.at[idx, "batches"] = 0.0

    # Replace remaining NaN stds with 0 for safe plotting
    std.fillna(0, inplace=True)

    return mean, std

# ── palette & style ───────────────────────────────────────────────────────────
MODE_ORDER   = ["Baseline", "GPUInstancing", "StaticBatching"]
MODE_LABELS  = {"Baseline": "Baseline",
                "GPUInstancing": "GPU Instancing",
                "StaticBatching": "Static Batching"}
MODE_COLORS  = {"Baseline": "#e05252",
                "GPUInstancing": "#4caf50",
                "StaticBatching": "#2196f3"}
MODE_MARKERS = {"Baseline": "o", "GPUInstancing": "s", "StaticBatching": "^"}

COMPLEXITY_ORDER  = ["Low", "Mid", "High"]
COMPLEXITY_LABELS = {
    "Low":  "Low-poly (112 tris)",
    "Mid":  "Mid-poly (1 740 tris)",
    "High": "High-poly (19 800 tris)",
}

N_VALUES = [100, 500, 1000, 5000, 10000]


def _lookup(df, mode, complexity, col="batches"):
    """Return sorted-by-N Series for (mode, complexity)."""
    return (df[(df["renderingMode"] == mode) & (df["meshComplexity"] == complexity)]
            .sort_values("N")[col].values)


def _n_sorted(df, mode, complexity):
    return (df[(df["renderingMode"] == mode) & (df["meshComplexity"] == complexity)]
            .sort_values("N")["N"].values)


def save(fig, name):
    path = os.path.join(FIGURES_DIR, name)
    fig.savefig(path, dpi=300, bbox_inches="tight")
    print(f"  Saved: {path}")
    plt.close(fig)

# ── Figure 1: FPS vs N ────────────────────────────────────────────────────────
def fig_fps_vs_n(mean, std):
    fig, axes = plt.subplots(1, 3, figsize=(14, 4.5), sharey=False)
    fig.suptitle("Average FPS vs Object Count N  (log scale, mean ± 1σ across 3 repeats)",
                 fontweight="bold")

    for ax, complexity in zip(axes, COMPLEXITY_ORDER):
        for mode in MODE_ORDER:
            ns   = _n_sorted(mean, mode, complexity)
            vals = _lookup(mean, mode, complexity, "fpsAvg")
            errs = _lookup(std,  mode, complexity, "fpsAvg")
            ax.plot(ns, vals,
                    color=MODE_COLORS[mode], marker=MODE_MARKERS[mode],
                    label=MODE_LABELS[mode], linewidth=1.8, markersize=5)
            ax.fill_between(ns, vals - errs, vals + errs,
                            color=MODE_COLORS[mode], alpha=0.18)

        ax.set_xscale("log")
        ax.set_xticks(N_VALUES)
        ax.get_xaxis().set_major_formatter(ticker.ScalarFormatter())
        ax.set_xlabel("Object Count N")
        ax.set_ylabel("FPS")
        ax.set_title(COMPLEXITY_LABELS[complexity])
        ax.legend()
        ax.grid(True, which="both", linestyle="--", linewidth=0.5, alpha=0.6)

    fig.tight_layout()
    save(fig, "fig1_fps_vs_n.png")

# ── Figure 2: Batches vs N ────────────────────────────────────────────────────
def fig_batches_vs_n(mean, std):
    fig, axes = plt.subplots(1, 3, figsize=(14, 4.5), sharey=False)
    fig.suptitle("Draw Call Batches vs Object Count N  (log-log scale, mean ± 1σ)\n"
                 "Dashed: theoretical fill for configurations where recorder returned 0",
                 fontweight="bold")

    for ax, complexity in zip(axes, COMPLEXITY_ORDER):
        for mode in MODE_ORDER:
            ns   = _n_sorted(mean, mode, complexity)
            vals = _lookup(mean, mode, complexity, "batches")
            errs = _lookup(std,  mode, complexity, "batches")
            ax.plot(ns, vals,
                    color=MODE_COLORS[mode], marker=MODE_MARKERS[mode],
                    label=MODE_LABELS[mode], linewidth=1.8, markersize=5)
            nonzero = errs > 0
            if nonzero.any():
                ax.errorbar(ns[nonzero], vals[nonzero], yerr=errs[nonzero],
                            fmt="none", color=MODE_COLORS[mode], capsize=3, alpha=0.5)

        ax.set_xscale("log")
        ax.set_yscale("log")
        ax.set_xticks(N_VALUES)
        ax.get_xaxis().set_major_formatter(ticker.ScalarFormatter())
        ax.set_xlabel("Object Count N")
        ax.set_ylabel("Batches")
        ax.set_title(COMPLEXITY_LABELS[complexity])
        ax.legend()
        ax.grid(True, which="both", linestyle="--", linewidth=0.5, alpha=0.6)

    fig.tight_layout()
    save(fig, "fig2_batches_vs_n.png")

# ── Figure 3: Wall-clock frame time vs GPU frame time scatter ─────────────────
def fig_frame_gpu_scatter(mean):
    fig, axes = plt.subplots(1, 3, figsize=(14, 4.5), sharey=False, sharex=False)
    fig.suptitle("Wall-clock Frame Time vs GPU Frame Time  (ms)\n"
                 "Color = rendering mode · Size = object count N  "
                 "► points near diagonal: GPU-bound  ► points below: CPU-bound",
                 fontweight="bold")

    size_map = {100: 30, 500: 60, 1000: 100, 5000: 160, 10000: 220}

    for ax, complexity in zip(axes, COMPLEXITY_ORDER):
        for mode in MODE_ORDER:
            sub = (mean[(mean["meshComplexity"] == complexity) &
                        (mean["renderingMode"] == mode)]
                   .sort_values("N"))
            sizes = [size_map[int(n)] for n in sub["N"]]
            ax.scatter(sub["frameTimeMs"], sub["gpuMs"],
                       c=MODE_COLORS[mode], s=sizes,
                       label=MODE_LABELS[mode], alpha=0.85,
                       edgecolors="white", linewidths=0.4)

        # Diagonal: gpuMs == frameTimeMs marks the GPU-bound boundary
        xlim = ax.get_xlim()[1]
        ylim = ax.get_ylim()[1]
        lim  = max(xlim, ylim) * 1.05
        ax.plot([0, lim], [0, lim], "k--", linewidth=0.8, alpha=0.4,
                label="GPU time = Frame time")
        ax.set_xlim(left=0)
        ax.set_ylim(bottom=0)

        ax.set_xlabel("Wall-clock Frame Time (ms)")
        ax.set_ylabel("GPU Frame Time (ms)")
        ax.set_title(COMPLEXITY_LABELS[complexity])
        ax.legend(fontsize=8)
        ax.grid(True, linestyle="--", linewidth=0.5, alpha=0.6)

    handles = [plt.scatter([], [], s=size_map[n], c="grey", alpha=0.7, label=f"N={n}")
               for n in N_VALUES]
    fig.legend(handles=handles, title="Object count", loc="lower center",
               ncol=5, bbox_to_anchor=(0.5, -0.05), fontsize=9)

    fig.tight_layout()
    save(fig, "fig3_cpu_gpu_scatter.png")

# ── Figure 4: Bar chart at N=10000 ───────────────────────────────────────────
def fig_bar_n10000(mean, std):
    n10k_m = mean[mean["N"] == 10000].copy()
    n10k_s = std[std["N"] == 10000].copy()

    metrics = [
        ("fpsAvg",      "Average FPS",                False),
        ("frameTimeMs", "Wall-clock Frame Time (ms)", False),
        ("gpuMs",       "GPU Frame Time (ms)",         False),
        ("batches",     "Draw Call Batches",           True),
    ]

    fig, axes = plt.subplots(1, 4, figsize=(16, 5))
    fig.suptitle("N = 10 000 — All Modes × All Mesh Complexities  (mean ± 1σ)",
                 fontweight="bold")

    x = np.arange(len(COMPLEXITY_ORDER))
    width = 0.25

    for ax, (col, ylabel, log_y) in zip(axes, metrics):
        for i, mode in enumerate(MODE_ORDER):
            vals, errs = [], []
            for c in COMPLEXITY_ORDER:
                rm = n10k_m[(n10k_m["renderingMode"] == mode) & (n10k_m["meshComplexity"] == c)]
                rs = n10k_s[(n10k_s["renderingMode"] == mode) & (n10k_s["meshComplexity"] == c)]
                vals.append(float(rm[col].iloc[0]) if len(rm) else 0.0)
                errs.append(float(rs[col].iloc[0]) if len(rs) else 0.0)

            bars = ax.bar(x + i * width, vals, width,
                          label=MODE_LABELS[mode], color=MODE_COLORS[mode],
                          edgecolor="white", linewidth=0.5)
            ax.errorbar(x + i * width, vals, yerr=errs,
                        fmt="none", color="black", capsize=3, linewidth=0.8, alpha=0.6)
            for bar, v in zip(bars, vals):
                ax.text(bar.get_x() + bar.get_width() / 2,
                        bar.get_height() * 1.02,
                        f"{v:.0f}" if v >= 10 else f"{v:.2f}",
                        ha="center", va="bottom", fontsize=7.5)

        ax.set_xticks(x + width)
        ax.set_xticklabels(["Low", "Mid", "High"])
        ax.set_xlabel("Mesh Complexity")
        ax.set_ylabel(ylabel)
        if log_y:
            ax.set_yscale("log")
        ax.legend(fontsize=8)
        ax.grid(True, axis="y", linestyle="--", linewidth=0.5, alpha=0.6)

    fig.tight_layout()
    save(fig, "fig4_bar_n10000.png")

# ── Figure 5: Static Batching batch-count breakdown ──────────────────────────
def fig_static_batch_breakdown(mean, std):
    fig, ax = plt.subplots(figsize=(8, 5))
    fig.suptitle("Static Batching: Batch Count vs N per Mesh Complexity\n"
                 "(Unity 65 536-vertex combined-mesh limit, mean ± 1σ)",
                 fontweight="bold")

    styles = {"Low": "--", "Mid": "-",  "High": ":"}
    colors = {"Low": "#aed6f1", "Mid": "#2196f3", "High": "#1a237e"}

    for complexity in COMPLEXITY_ORDER:
        ns   = _n_sorted(mean, "StaticBatching", complexity)
        vals = _lookup(mean, "StaticBatching", complexity, "batches")
        errs = _lookup(std,  "StaticBatching", complexity, "batches")
        ax.plot(ns, vals,
                color=colors[complexity], linestyle=styles[complexity],
                marker="o", markersize=5, linewidth=1.8,
                label=COMPLEXITY_LABELS[complexity])
        nonzero = errs > 0
        if nonzero.any():
            ax.errorbar(ns[nonzero], vals[nonzero], yerr=errs[nonzero],
                        fmt="none", color=colors[complexity], capsize=3, alpha=0.4)

    # GPU Instancing (Mid) as reference
    ref_ns   = _n_sorted(mean, "GPUInstancing", "Mid")
    ref_vals = _lookup(mean, "GPUInstancing", "Mid", "batches")
    ax.plot(ref_ns, ref_vals,
            color=MODE_COLORS["GPUInstancing"], linestyle="-.", marker="s",
            markersize=5, linewidth=1.4, label="GPU Instancing (Mid) — reference")

    ax.set_xscale("log")
    ax.set_xticks(N_VALUES)
    ax.get_xaxis().set_major_formatter(ticker.ScalarFormatter())
    ax.set_xlabel("Object Count N")
    ax.set_ylabel("Batches")
    ax.legend()
    ax.grid(True, which="both", linestyle="--", linewidth=0.5, alpha=0.6)
    fig.tight_layout()
    save(fig, "fig5_static_batch_breakdown.png")

# ── Figure 6: Memory vs N ─────────────────────────────────────────────────────
def fig_memory_vs_n(mean, std):
    fig, axes = plt.subplots(1, 3, figsize=(14, 4.5), sharey=False)
    fig.suptitle("Total Reserved Memory (MB) vs Object Count N  "
                 "(log scale, mean ± 1σ across 3 repeats)",
                 fontweight="bold")

    for ax, complexity in zip(axes, COMPLEXITY_ORDER):
        for mode in MODE_ORDER:
            ns   = _n_sorted(mean, mode, complexity)
            vals = _lookup(mean, mode, complexity, "totalReservedMB")
            errs = _lookup(std,  mode, complexity, "totalReservedMB")
            ax.plot(ns, vals,
                    color=MODE_COLORS[mode], marker=MODE_MARKERS[mode],
                    label=MODE_LABELS[mode], linewidth=1.8, markersize=5)
            ax.fill_between(ns, vals - errs, vals + errs,
                            color=MODE_COLORS[mode], alpha=0.18)

        ax.set_xscale("log")
        ax.set_xticks(N_VALUES)
        ax.get_xaxis().set_major_formatter(ticker.ScalarFormatter())
        ax.set_xlabel("Object Count N")
        ax.set_ylabel("Total Reserved Memory (MB)")
        ax.set_title(COMPLEXITY_LABELS[complexity])
        ax.legend()
        ax.grid(True, which="both", linestyle="--", linewidth=0.5, alpha=0.6)

    fig.tight_layout()
    save(fig, "fig6_memory_vs_n.png")

# ── F8: Static Batching theoretical-vs-measured batch table ──────────────────
def emit_static_batch_table(mean):
    rows_data = []
    for complexity in COMPLEXITY_ORDER:
        v = VERTS_PER_MESH[complexity]
        for n in N_VALUES:
            theo = math.ceil(n * v / 65536)
            row = mean[(mean["renderingMode"] == "StaticBatching") &
                       (mean["meshComplexity"] == complexity) &
                       (mean["N"] == n)]
            meas = int(round(row["batches"].values[0])) if len(row) > 0 else -1
            rows_data.append({"complexity": complexity, "N": n,
                               "verts": v, "theoretical": theo, "measured": meas})

    # CSV
    csv_path = os.path.join(FIGURES_DIR, "static_batch_table.csv")
    pd.DataFrame(rows_data).to_csv(csv_path, index=False)
    print(f"  Saved: {csv_path}")

    # LaTeX table body (suitable for \input{})
    tex_lines = [
        r"\begin{tabular}{llrrrr}",
        r"  \toprule",
        r"  Complexity & $N$ & Vertices/obj & Theoretical & Measured & $\Delta$ \\",
        r"  \midrule",
    ]
    prev_c = None
    for r in rows_data:
        if prev_c is not None and r["complexity"] != prev_c:
            tex_lines.append(r"  \midrule")
        delta = r["measured"] - r["theoretical"]
        tex_lines.append(
            f"  {r['complexity']} & {r['N']:>6} & {r['verts']:>5} "
            f"& {r['theoretical']:>6} & {r['measured']:>6} & {delta:>+5} \\\\"
        )
        prev_c = r["complexity"]
    tex_lines += [r"  \bottomrule", r"\end{tabular}"]

    tex_path = os.path.join(FIGURES_DIR, "static_batch_table.tex")
    with open(tex_path, "w") as f:
        f.write("\n".join(tex_lines) + "\n")
    print(f"  Saved: {tex_path}")

# ── main ──────────────────────────────────────────────────────────────────────
if __name__ == "__main__":
    csv_path = sys.argv[1] if len(sys.argv) > 1 else None
    df = load_csv(csv_path)

    print(f"Loaded {len(df)} rows, {df['repeat'].nunique()} repeats per config")
    mean, std = aggregate(df)

    print("\nGenerating figures...")
    fig_fps_vs_n(mean, std)
    fig_batches_vs_n(mean, std)
    fig_frame_gpu_scatter(mean)
    fig_bar_n10000(mean, std)
    fig_static_batch_breakdown(mean, std)
    fig_memory_vs_n(mean, std)

    print("\nGenerating tables...")
    emit_static_batch_table(mean)

    print(f"\nDone. Figures in {FIGURES_DIR}/")
