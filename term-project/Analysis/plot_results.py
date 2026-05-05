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

def load_csv(path=None):
    if path:
        return pd.read_csv(path)
    csvs = sorted(glob.glob(os.path.join(RESULTS_DIR, "results_*.csv")))
    if not csvs:
        raise FileNotFoundError(f"No results_*.csv found in {RESULTS_DIR}")
    print(f"Loading: {csvs[-1]}")
    return pd.read_csv(csvs[-1])

# ── palette & style ───────────────────────────────────────────────────────────
MODE_ORDER   = ["Baseline", "GPUInstancing", "StaticBatching"]
MODE_LABELS  = {"Baseline": "Baseline", "GPUInstancing": "GPU Instancing", "StaticBatching": "Static Batching"}
MODE_COLORS  = {"Baseline": "#e05252", "GPUInstancing": "#4caf50", "StaticBatching": "#2196f3"}
MODE_MARKERS = {"Baseline": "o",       "GPUInstancing": "s",         "StaticBatching": "^"}

COMPLEXITY_ORDER  = ["Low", "Mid", "High"]
COMPLEXITY_LABELS = {"Low": "Low-poly (~112 tris)", "Mid": "Mid-poly (~1 740 tris)", "High": "High-poly (~19 800 tris)"}

N_VALUES = [100, 500, 1000, 5000, 10000]

def save(fig, name):
    path = os.path.join(FIGURES_DIR, name)
    fig.savefig(path, dpi=300, bbox_inches="tight")
    print(f"  Saved: {path}")
    plt.close(fig)

# ── Figure 1: FPS vs N ────────────────────────────────────────────────────────
def fig_fps_vs_n(df):
    fig, axes = plt.subplots(1, 3, figsize=(14, 4.5), sharey=False)
    fig.suptitle("Average FPS vs Object Count N  (log scale)", fontweight="bold")

    for ax, complexity in zip(axes, COMPLEXITY_ORDER):
        sub = df[df["meshComplexity"] == complexity]
        for mode in MODE_ORDER:
            d = sub[sub["renderingMode"] == mode].sort_values("N")
            ax.plot(d["N"], d["fpsAvg"],
                    color=MODE_COLORS[mode], marker=MODE_MARKERS[mode],
                    label=MODE_LABELS[mode], linewidth=1.8, markersize=5)
            ax.fill_between(d["N"], d["fps1pcLow"], d["fpsAvg"],
                            color=MODE_COLORS[mode], alpha=0.12)

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
def fig_batches_vs_n(df):
    fig, axes = plt.subplots(1, 3, figsize=(14, 4.5), sharey=False)
    fig.suptitle("Draw Call Batches vs Object Count N  (log-log scale)", fontweight="bold")

    for ax, complexity in zip(axes, COMPLEXITY_ORDER):
        sub = df[df["meshComplexity"] == complexity]
        for mode in MODE_ORDER:
            d = sub[sub["renderingMode"] == mode].sort_values("N")
            ax.plot(d["N"], d["batches"],
                    color=MODE_COLORS[mode], marker=MODE_MARKERS[mode],
                    label=MODE_LABELS[mode], linewidth=1.8, markersize=5)

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

# ── Figure 3: CPU ms vs GPU ms scatter ───────────────────────────────────────
def fig_cpu_gpu_scatter(df):
    fig, axes = plt.subplots(1, 3, figsize=(14, 4.5), sharey=False, sharex=False)
    fig.suptitle("CPU Frame Time vs GPU Frame Time  (ms)\n"
                 "Color = rendering mode · Size = object count N", fontweight="bold")

    size_map = {100: 30, 500: 60, 1000: 100, 5000: 160, 10000: 220}

    for ax, complexity in zip(axes, COMPLEXITY_ORDER):
        sub = df[df["meshComplexity"] == complexity]
        for mode in MODE_ORDER:
            d = sub[sub["renderingMode"] == mode].sort_values("N")
            sizes = [size_map[n] for n in d["N"]]
            sc = ax.scatter(d["cpuMs"], d["gpuMs"],
                            c=MODE_COLORS[mode], s=sizes,
                            label=MODE_LABELS[mode], alpha=0.85, edgecolors="white", linewidths=0.4)

        # Diagonal (CPU == GPU — equal bottleneck line)
        lim = max(ax.get_xlim()[1], ax.get_ylim()[1])
        ax.plot([0, lim], [0, lim], "k--", linewidth=0.8, alpha=0.4, label="CPU = GPU")

        ax.set_xlabel("CPU Frame Time (ms)")
        ax.set_ylabel("GPU Frame Time (ms)")
        ax.set_title(COMPLEXITY_LABELS[complexity])
        ax.legend(fontsize=8)
        ax.grid(True, linestyle="--", linewidth=0.5, alpha=0.6)

    # Size legend
    handles = [plt.scatter([], [], s=size_map[n], c="grey", alpha=0.7, label=f"N={n}")
               for n in N_VALUES]
    fig.legend(handles=handles, title="Object count", loc="lower center",
               ncol=5, bbox_to_anchor=(0.5, -0.05), fontsize=9)

    fig.tight_layout()
    save(fig, "fig3_cpu_gpu_scatter.png")

# ── Figure 4: Bar chart at N=10000 ───────────────────────────────────────────
def fig_bar_n10000(df):
    n10k = df[df["N"] == 10000].copy()

    metrics = [
        ("fpsAvg",  "Average FPS",           False),
        ("cpuMs",   "CPU Frame Time (ms)",    False),
        ("gpuMs",   "GPU Frame Time (ms)",    False),
        ("batches", "Draw Call Batches",      True),
    ]

    fig, axes = plt.subplots(1, 4, figsize=(16, 5))
    fig.suptitle("N = 10 000 — All Modes × All Mesh Complexities", fontweight="bold")

    x = np.arange(len(COMPLEXITY_ORDER))
    width = 0.25

    for ax, (col, ylabel, log_y) in zip(axes, metrics):
        for i, mode in enumerate(MODE_ORDER):
            vals = [
                n10k[(n10k["renderingMode"] == mode) & (n10k["meshComplexity"] == c)][col].values[0]
                for c in COMPLEXITY_ORDER
            ]
            bars = ax.bar(x + i * width, vals, width,
                          label=MODE_LABELS[mode], color=MODE_COLORS[mode],
                          edgecolor="white", linewidth=0.5)
            # value labels on bars
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
def fig_static_batch_breakdown(df):
    """Shows how Static Batching batch count grows with complexity — the 65K vertex limit effect."""
    fig, ax = plt.subplots(figsize=(8, 5))
    fig.suptitle("Static Batching: Batch Count vs N per Mesh Complexity\n"
                 "(Unity 65 536-vertex combined-mesh limit)", fontweight="bold")

    styles = {"Low": "--", "Mid": "-", "High": ":"}
    colors = {"Low": "#aed6f1", "Mid": "#2196f3", "High": "#1a237e"}

    sub = df[df["renderingMode"] == "StaticBatching"]
    for complexity in COMPLEXITY_ORDER:
        d = sub[sub["meshComplexity"] == complexity].sort_values("N")
        ax.plot(d["N"], d["batches"],
                color=colors[complexity], linestyle=styles[complexity],
                marker="o", markersize=5, linewidth=1.8,
                label=COMPLEXITY_LABELS[complexity])

    # Reference: GPU Instancing (Mid)
    ref = df[(df["renderingMode"] == "GPUInstancing") & (df["meshComplexity"] == "Mid")].sort_values("N")
    ax.plot(ref["N"], ref["batches"],
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

# ── main ──────────────────────────────────────────────────────────────────────
if __name__ == "__main__":
    csv_path = sys.argv[1] if len(sys.argv) > 1 else None
    df = load_csv(csv_path)

    print("\nGenerating figures...")
    fig_fps_vs_n(df)
    fig_batches_vs_n(df)
    fig_cpu_gpu_scatter(df)
    fig_bar_n10000(df)
    fig_static_batch_breakdown(df)

    print(f"\nAll figures saved to {FIGURES_DIR}/")
