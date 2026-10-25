// Dev-server proxy: the browser talks only to http://localhost:4200, and /api calls are
// forwarded to the ASP.NET Core API. One origin means the session cookie just works and no
// CORS configuration is needed.
//
// OPSHUB_API_URL overrides the target (default: the API's "https" launch profile).
// `secure: false` only skips Node's certificate check for the local dev certificate;
// it is a development-only setting and never used in production.
const target = process.env.OPSHUB_API_URL ?? 'https://localhost:7003';

export default {
  '/api': { target, secure: false, changeOrigin: false, logLevel: 'warn' },
  '/health': { target, secure: false },
};
