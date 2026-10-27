#!/usr/bin/env python3
"""A static file server that honours Range requests, for testing the browser build locally.

`python -m http.server` ignores `Range:` and answers with the whole body, which means the music
archive cannot be tested against it - the fetch reads a zip over ranges and would pull 148 MB for
its first request. GitHub Pages and any production static host answer ranges, so this only has to
cover the local case.

Usage: serve.py [port] [root]
"""

import functools
import http.server
import os
import re
import sys


class RangeHandler(http.server.SimpleHTTPRequestHandler):
    def send_head(self):
        path = self.translate_path(self.path)
        if os.path.isdir(path):
            return super().send_head()

        try:
            f = open(path, 'rb')
        except OSError:
            self.send_error(404, 'File not found')
            return None

        size = os.fstat(f.fileno()).st_size
        range_header = self.headers.get('Range')
        if not range_header:
            self.send_response(200)
            self.send_header('Content-Type', self.guess_type(path))
            self.send_header('Content-Length', str(size))
            self.send_header('Accept-Ranges', 'bytes')
            self.end_headers()
            return f

        match = re.match(r'bytes=(\d*)-(\d*)', range_header)
        if not match:
            self.send_error(416, 'Bad range')
            f.close()
            return None

        start, end = match.groups()
        if start:
            start = int(start)
            end = int(end) if end else size - 1
        else:
            # A suffix range asks for the last N bytes, which is how a zip reader finds the end
            # of the central directory.
            length = int(end)
            start, end = max(0, size - length), size - 1
        end = min(end, size - 1)

        if start > end:
            self.send_error(416, 'Not satisfiable')
            f.close()
            return None

        self.send_response(206)
        self.send_header('Content-Type', self.guess_type(path))
        self.send_header('Content-Range', f'bytes {start}-{end}/{size}')
        self.send_header('Content-Length', str(end - start + 1))
        self.send_header('Accept-Ranges', 'bytes')
        self.end_headers()
        f.seek(start)
        return _LimitedReader(f, end - start + 1)

    def copyfile(self, source, outputfile):
        remaining = getattr(source, 'remaining', None)
        if remaining is None:
            return super().copyfile(source, outputfile)
        while remaining > 0:
            chunk = source.read(min(65536, remaining))
            if not chunk:
                break
            outputfile.write(chunk)
            remaining -= len(chunk)

    def log_message(self, fmt, *args):
        pass


class _LimitedReader:
    def __init__(self, f, limit):
        self._f = f
        self.remaining = limit

    def read(self, n=-1):
        if self.remaining <= 0:
            return b''
        if n < 0 or n > self.remaining:
            n = self.remaining
        data = self._f.read(n)
        self.remaining -= len(data)
        return data

    def close(self):
        self._f.close()


def main() -> int:
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8099
    root = sys.argv[2] if len(sys.argv) > 2 else '.'

    handler = functools.partial(RangeHandler, directory=root)
    with http.server.ThreadingHTTPServer(('127.0.0.1', port), handler) as httpd:
        print(f'serving {os.path.abspath(root)} on http://127.0.0.1:{port} (ranges honoured)')
        httpd.serve_forever()
    return 0


if __name__ == '__main__':
    sys.exit(main())
