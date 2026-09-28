/**
 * Proxies public catalog share pages and app-link association files to the MovieApp API.
 * Static marketing/legal pages continue to be served from the ASSETS binding.
 */
export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const shouldProxy =
      /^\/(movie|tv)\/[^/]+/.test(url.pathname) ||
      url.pathname === "/.well-known/apple-app-site-association" ||
      url.pathname === "/.well-known/assetlinks.json";

    const apiOrigin = env.CATALOG_SHARE_API_ORIGIN?.trim();
    if (shouldProxy && apiOrigin) {
      const target = new URL(`${url.pathname}${url.search}`, apiOrigin.replace(/\/$/, ""));
      const proxied = new Request(target.toString(), {
        method: request.method,
        headers: request.headers,
        redirect: "manual",
      });
      return fetch(proxied);
    }

    return env.ASSETS.fetch(request);
  },
};
