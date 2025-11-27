export function setupSEO(router) {
  router.afterEach((to) => {
    const title = to?.meta?.title ?? 'ASG';
    if (title) document.title = title;
    const description = to?.meta?.description ?? '';
    if (description) {
      let el = document.querySelector('meta[name="description"]');
      if (!el) {
        el = document.createElement('meta');
        el.setAttribute('name', 'description');
        document.head.appendChild(el);
      }
      el.setAttribute('content', description);
    }
  });
}
