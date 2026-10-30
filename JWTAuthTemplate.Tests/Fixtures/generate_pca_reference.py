"""Independent fixture: pip install numpy openpyxl; python generate_pca_reference.py."""
import json
from pathlib import Path
import numpy as np
import openpyxl

root = Path(__file__).resolve().parent
rows = list(openpyxl.load_workbook(root / "NiCu100.xlsx", read_only=True, data_only=True).worksheets[0].values)
cols = max(i + 1 for row in rows for i, value in enumerate(row) if value is not None)
x = np.array([[float(v) for v in row[:cols]] for row in rows[1:]
              if all(isinstance(v, (int, float)) and np.isfinite(v) for v in row[:cols])])
active = np.ptp(x, axis=0) != 0
z = np.zeros_like(x)
z[:, active] = (x[:, active] - x[:, active].mean(axis=0)) / x[:, active].std(axis=0, ddof=0)
eigenvalues = np.maximum(np.linalg.eigvalsh(z.T @ z / (len(x) - 1))[::-1], 0)
ratios = eigenvalues / eigenvalues.sum()
(root / "NiCu100.numpy.json").write_text(json.dumps({
    "rowCount": len(x), "columnCount": cols, "ratios": ratios.tolist(),
    "reference": "NumPy eigvalsh; population standardization; exact constant columns set to zero; covariance divisor n-1"
}, indent=2), encoding="utf-8")
print(f"{len(x)} x {cols}; first ratios: {ratios[:5]}")
