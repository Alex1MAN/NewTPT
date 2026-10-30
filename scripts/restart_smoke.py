"""Только отдельный тестовый стек: pip install requests.
python scripts/restart_smoke.py --container newtpt-test-api --allow-container-restart
"""
import argparse
import hashlib
import json
import subprocess
import time
import uuid
import requests

parser = argparse.ArgumentParser()
parser.add_argument("--url", default="http://localhost:5505")
parser.add_argument("--container", required=True)
parser.add_argument("--sudo", action="store_true")
parser.add_argument("--allow-container-restart", action="store_true")
args = parser.parse_args()
if not args.allow_container_restart or not args.container.startswith("newtpt-test-"):
    parser.error("Explicit restart approval and a newtpt-test-* container name are required.")

client = requests.Session()
name = "restart" + uuid.uuid4().hex
password = "TestPassword123!"

def check(response):
    response.raise_for_status()
    result = response.json()
    assert result is True or result.get("success") is True, result
    return result

check(client.post(args.url + "/Auth/Register", json={
    "username": name, "email": name + "@example.test",
    "password": password, "confirmPassword": password}, timeout=30))
login = check(client.post(args.url + "/Auth/Login", json={"username": name, "password": password}, timeout=30))
bucket = login["data"]["user"]["id"]
metadata = {"bucketName": bucket, "uploadId": "restart-001", "fileName": "restart.bin", "totalChunks": 2}
for index, content in [(1, b"after restart"), (0, b"before ")]:
    check(client.post(args.url + "/Minio/UploadChunk", data={**metadata, "chunkIndex": index},
                      files={"chunk": ("part.bin", content)}, timeout=30))

command = (["sudo"] if args.sudo else []) + ["docker", "restart", args.container]
subprocess.run(command, check=True, capture_output=True, text=True)
deadline = time.monotonic() + 30
while True:
    try:
        if client.get(args.url + "/analysis/methods", timeout=2).ok:
            break
    except requests.RequestException:
        pass
    if time.monotonic() > deadline:
        raise RuntimeError("API did not restart within 30 seconds.")
    time.sleep(0.25)

first = check(client.post(args.url + "/Minio/CompleteUpload", json=metadata, timeout=30))
second = check(client.post(args.url + "/Minio/CompleteUpload", json=metadata, timeout=30))
assert first["documentId"] == second["documentId"]
response = client.get(args.url + "/Minio/GetFileContent",
                      params={"bucketName": bucket, "fileName": first["minioObjectName"]}, timeout=30)
response.raise_for_status()
assert response.content == b"before after restart"
assert response.headers["X-Success"] == "true"
print(json.dumps({"passed": True, "scenario": "API process restart between UploadChunk and CompleteUpload",
                  "sameDocumentOnRetry": True, "sha256": hashlib.sha256(response.content).hexdigest()}, indent=2))
