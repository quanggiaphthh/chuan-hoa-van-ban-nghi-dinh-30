#!/usr/bin/env python3
"""Executable local auth/bind smoke test; requires the built .NET 10 host."""
import os
import json
import hashlib
from pathlib import Path
import socket
import subprocess
import time
from io import BytesIO
from urllib.parse import urlencode
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen
from zipfile import ZipFile


ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "src/DocumentProcessor.Host/DocumentProcessor.Host.csproj"
TOKEN = "local-test-only-processor-token-with-at-least-32-bytes"
DOCX_MIME = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"


def launch(environment):
    return subprocess.Popen(
        ["dotnet", "run", "--no-build", "-c", "Release", "--project", str(PROJECT)],
        cwd=ROOT, env=environment, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
    )


def stop(process):
    if process.poll() is None:
        process.terminate()
        try:
            process.communicate(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.communicate(timeout=10)


def rejected_startup(environment):
    process = launch(environment)
    try:
        output, _ = process.communicate(timeout=15)
        assert process.returncode != 0, "unsafe host configuration started"
        assert TOKEN.encode() not in output, "shared token leaked in startup logs"
    finally:
        stop(process)


def request(url, token=None):
    headers = {"Authorization": f"Bearer {token}"} if token else {}
    try:
        with urlopen(Request(url, headers=headers), timeout=2) as response:
            return response.status
    except HTTPError as error:
        return error.code


def post_error(url, data, content_length=None):
    headers = {
        "Authorization": f"Bearer {TOKEN}",
        "Content-Type": "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "X-Correlation-ID": "host-boundary-test",
    }
    if content_length is not None:
        headers["Content-Length"] = str(content_length)
    try:
        with urlopen(Request(url, data=data, headers=headers, method="POST"), timeout=5) as response:
            return response.status, json.load(response)
    except HTTPError as error:
        return error.code, json.load(error)


def post_document(url, data):
    headers = {
        "Authorization": f"Bearer {TOKEN}",
        "Content-Type": DOCX_MIME,
        "X-Correlation-ID": "host-round-trip-test",
    }
    with urlopen(Request(url, data=data, headers=headers, method="POST"), timeout=30) as response:
        return response.status, response.headers, response.read()


def main():
    environment = os.environ.copy()
    environment.pop("DOCUMENT_PROCESSOR_SHARED_TOKEN", None)
    environment.pop("ASPNETCORE_URLS", None)
    environment["ASPNETCORE_ENVIRONMENT"] = "Development"
    rejected_startup(environment)

    environment["DOCUMENT_PROCESSOR_SHARED_TOKEN"] = TOKEN
    environment["ASPNETCORE_URLS"] = "http://0.0.0.0:5099"
    rejected_startup(environment)

    environment["DOCUMENT_PROCESSOR_SHARED_TOKEN"] = "short-token"
    environment["ASPNETCORE_URLS"] = "http://127.0.0.1:5099"
    rejected_startup(environment)
    environment["DOCUMENT_PROCESSOR_SHARED_TOKEN"] = TOKEN

    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        port = sock.getsockname()[1]
    environment["ASPNETCORE_URLS"] = f"http://127.0.0.1:{port}"
    process = launch(environment)
    try:
        url = f"http://127.0.0.1:{port}/health"
        for _ in range(100):
            if process.poll() is not None:
                raise AssertionError("processor exited before local auth verification")
            try:
                status = request(url)
                break
            except URLError:
                time.sleep(0.1)
        else:
            raise AssertionError("processor did not bind loopback before deadline")
        assert status == 401, f"missing token returned {status}"
        assert request(url, "wrong-token") == 401, "wrong token was accepted"
        assert request(url, TOKEN) == 200, "correct token was rejected"
        capabilities = Request(f"http://127.0.0.1:{port}/capabilities",
                               headers={"Authorization": f"Bearer {TOKEN}"})
        with urlopen(capabilities, timeout=5) as response:
            assert "paragraph.alignment.direct" in json.load(response)["operations"]
        status, payload = post_error(f"http://127.0.0.1:{port}/inspect", b"not a DOCX")
        assert status == 422 and payload["code"] == "MALFORMED_DOCX", (status, payload)
        assert payload["correlationId"] == "host-boundary-test"
        assert TOKEN not in json.dumps(payload) and str(ROOT) not in json.dumps(payload)
        status, payload = post_error(f"http://127.0.0.1:{port}/inspect", b"x", 20 * 1024 * 1024 + 1)
        assert status == 413 and payload["code"] == "INPUT_TOO_LARGE", (status, payload)

        fixture = ROOT / "fixtures/docx/23-direct-alignment.docx"
        source = fixture.read_bytes()
        source_digest = hashlib.sha256(source).hexdigest()
        status, _, inspection_bytes = post_document(f"http://127.0.0.1:{port}/inspect", source)
        inspection = json.loads(inspection_bytes)
        assert status == 200 and inspection["safeToMutate"], (status, inspection)
        target = next(p for p in inspection["paragraphs"] if p["paragraphId"] == "p1")
        assert target["directAlignment"] == "LEFT", target
        query = urlencode({"sourceSha256": source_digest, "paragraphId": "p1",
                           "expectedBefore": "LEFT", "desiredAfter": "CENTER"})
        status, response_headers, output = post_document(f"http://127.0.0.1:{port}/apply?{query}", source)
        assert status == 200 and response_headers["Content-Type"] == DOCX_MIME
        assert response_headers["X-Output-SHA256"] == hashlib.sha256(output).hexdigest()
        assert response_headers["X-Document-Reopened"] == "true"
        assert response_headers["X-Document-Revalidated"] == "true"
        assert output != source and hashlib.sha256(fixture.read_bytes()).hexdigest() == source_digest
        with ZipFile(BytesIO(output)) as package:
            assert package.testzip() is None and "word/document.xml" in package.namelist()
        status, _, reopened_bytes = post_document(f"http://127.0.0.1:{port}/inspect", output)
        reopened = json.loads(reopened_bytes)
        assert status == 200
        assert next(p for p in reopened["paragraphs"] if p["paragraphId"] == "p1")["directAlignment"] == "CENTER"
    finally:
        stop(process)
    print("PROCESSOR HOST BOUNDARY: PASS (loopback auth, safe errors, and direct-alignment round-trip)")


if __name__ == "__main__":
    main()
