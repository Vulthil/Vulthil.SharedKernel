// DocFX modern-template hooks. Adds a version selector to the navbar so readers
// can switch between the released docs (the default) and the pre-release "main"
// build. The manifest lives at the site root; see docs/design/docs-versioning.md.
export default {
  start: () => {},
  ready: () => {
    renderVersionSelector();
  },
};

const REPO_BASE = '/Vulthil.SharedKernel/';

async function renderVersionSelector() {
  let manifest;
  try {
    const response = await fetch(`${REPO_BASE}versions.json`, { cache: 'no-cache' });
    if (!response.ok) {
      return;
    }
    manifest = await response.json();
  } catch {
    return;
  }

  const versions = manifest.versions || [];
  if (versions.length === 0) {
    return;
  }

  const current = currentSlug();
  const select = document.createElement('select');
  select.className = 'form-select form-select-sm version-selector';
  select.setAttribute('aria-label', 'Documentation version');

  for (const version of versions) {
    const option = document.createElement('option');
    option.value = version.slug;
    option.textContent = labelFor(version, manifest.default);
    option.selected = version.slug === current;
    select.appendChild(option);
  }

  select.addEventListener('change', () => {
    window.location.href = `${REPO_BASE}${select.value}/`;
  });

  mountSelector(select);
}

function currentSlug() {
  const path = window.location.pathname;
  if (!path.startsWith(REPO_BASE)) {
    return '';
  }
  return path.slice(REPO_BASE.length).split('/')[0];
}

function labelFor(version, defaultSlug) {
  if (version.slug === defaultSlug) {
    return `${version.slug} (latest)`;
  }
  if (version.prerelease) {
    return `${version.slug} (pre-release)`;
  }
  return version.slug;
}

function mountSelector(select) {
  const wrapper = document.createElement('div');
  wrapper.className = 'version-selector-wrapper';
  wrapper.appendChild(select);

  const host = document.querySelector('nav.navbar .container-xxl')
    || document.querySelector('nav.navbar')
    || document.querySelector('header');
  if (host) {
    host.appendChild(wrapper);
  }
}
