const http = require('http');
const fs = require('fs');
const path = require('path');
const { exec } = require('child_process');

const PORT = 8080;
const ROOT_DIR = path.join(__dirname, 'Builds', 'Webgl');

const MIME_TYPES = {
  '.html': 'text/html',
  '.js': 'text/javascript',
  '.css': 'text/css',
  '.wasm': 'application/wasm',
  '.data': 'application/octet-stream',
  '.ico': 'image/x-icon',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.json': 'application/json'
};

const server = http.createServer((req, res) => {
  let cleanUrl = decodeURIComponent(req.url.split('?')[0]);
  if (cleanUrl === '/' || cleanUrl === '') {
    cleanUrl = '/index.html';
  }

  const filePath = path.normalize(path.join(ROOT_DIR, cleanUrl));

  // Security check: ensure path stays inside ROOT_DIR
  if (!filePath.startsWith(ROOT_DIR)) {
    res.writeHead(403);
    return res.end('Access denied');
  }

  if (!fs.existsSync(filePath) || fs.statSync(filePath).isDirectory()) {
    res.writeHead(404);
    return res.end('File not found: ' + cleanUrl);
  }

  const isBrotli = filePath.endsWith('.br');
  const isGzip = filePath.endsWith('.gz');

  let baseExt = path.extname(filePath);
  if (isBrotli) {
    baseExt = path.extname(filePath.slice(0, -3));
  } else if (isGzip) {
    baseExt = path.extname(filePath.slice(0, -3));
  }

  const contentType = MIME_TYPES[baseExt] || 'application/octet-stream';
  const headers = {
    'Content-Type': contentType,
    'Cross-Origin-Opener-Policy': 'same-origin',
    'Cross-Origin-Embedder-Policy': 'require-corp'
  };

  if (isBrotli) {
    headers['Content-Encoding'] = 'br';
  } else if (isGzip) {
    headers['Content-Encoding'] = 'gzip';
  }

  res.writeHead(200, headers);
  fs.createReadStream(filePath).pipe(res);
});

server.listen(PORT, () => {
  const url = `http://localhost:${PORT}/`;
  console.log(`\n==================================================`);
  console.log(` Unity WebGL Local Server running at:`);
  console.log(` ${url}`);
  console.log(` Serving from: ${ROOT_DIR}`);
  console.log(` Press Ctrl+C to stop the server.`);
  console.log(`==================================================\n`);

  // Open the browser automatically on Windows
  exec(`start ${url}`);
});

