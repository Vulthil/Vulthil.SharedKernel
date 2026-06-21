// DocFX modern-template hooks. Adds a version selector to the navbar so readers
// can switch between the released docs (the default) and the pre-release "main"
// build. Switching keeps the reader on the same page when that page also exists
// in the target version, otherwise it falls back to the version's home page.
// The manifest (versions.json) lives at the site root and is produced by
// eng/update_versions.py during each docs deploy.
//
// DocFX 2.78's modern template only invokes the `start` hook from this module
// (it has no `ready` hook), so the selector is mounted from `start`. The navbar
// shell is static markup and docfx.min.js loads as a deferred module, so the
// mount target already exists by the time `start` runs.
export default {
  start: () => {
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
    switchToVersion(select.value);
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

function currentSubpath() {
  const slug = currentSlug();
  if (!slug) {
    return '';
  }
  const prefix = `${REPO_BASE}${slug}/`;
  const path = window.location.pathname;
  return path.startsWith(prefix) ? path.slice(prefix.length) : '';
}

async function switchToVersion(slug) {
  const target = `${REPO_BASE}${slug}/`;
  const subpath = currentSubpath();

  if (subpath) {
    const candidate = `${target}${subpath}`;
    if (await pageExists(candidate)) {
      window.location.href = `${candidate}${window.location.hash}`;
      return;
    }
  }

  window.location.href = target;
}

async function pageExists(url) {
  try {
    const response = await fetch(url, { method: 'HEAD' });
    return response.ok;
  } catch {
    return false;
  }
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
