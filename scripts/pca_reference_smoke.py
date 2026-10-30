"""pip install requests numpy openpyxl
python scripts/pca_reference_smoke.py path/to/NiCu100.xlsx path/to/NiCu1000.xlsx
"""
import argparse
import json
from pathlib import Path
import numpy as np
import openpyxl
import requests

parser = argparse.ArgumentParser()
parser.add_argument("files", nargs="+", type=Path)
parser.add_argument("--url", default="http://localhost:5505")
args = parser.parse_args()
results = []
for path in args.files:
    rows = list(openpyxl.load_workbook(path, read_only=True, data_only=True).worksheets[0].values)
    cols = max(i + 1 for row in rows for i, value in enumerate(row) if value is not None)
    numeric = []
    for row in rows[1:]:
        try:
            values = [float(v) for v in row[:cols]]
            if all(np.isfinite(values)):
                numeric.append(values)
        except (ValueError, TypeError):
            pass
    x = np.asarray(numeric)
    active = np.ptp(x, axis=0) != 0
    z = np.zeros_like(x)
    z[:, active] = (x[:, active] - x[:, active].mean(axis=0)) / x[:, active].std(axis=0, ddof=0)
    eigenvalues = np.maximum(np.linalg.eigvalsh(z.T @ z / (len(x) - 1))[::-1], 0)
    expected = eigenvalues / eigenvalues.sum()
    with path.open("rb") as stream:
        response = requests.post(args.url + "/analysis/AnalyzeExcel", files={"file": (path.name, stream)}, timeout=180)
    response.raise_for_status()
    payload = response.json()
    assert payload["success"] is True
    data = payload["data"]
    assert data["rowCount"] == len(x) and data["componentsCount"] == cols
    difference = float(np.max(np.abs(np.array(data["explainedVarianceRatio"]) - expected)))
    assert difference < 1e-10, difference
    results.append({"file": path.name, "passed": True, "rows": len(x), "columns": cols,
                    "maxAbsoluteRatioError": difference, "tolerance": 1e-10})
print(json.dumps(results, indent=2))
