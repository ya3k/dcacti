import { describe, it, expect, beforeAll } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

/**
 * CSS viewport foundation tests.
 *
 * The stylesheet is asserted as text because jsdom does not perform layout.
 * These checks guard the game-viewport contract: the document fills the
 * viewport and never scrolls.
 */
describe('Game viewport CSS foundation', () => {
  let css: string;

  beforeAll(() => {
    const raw = readFileSync(resolve(__dirname, '../src/app/App.css'), 'utf8');
    // Strip comments so selectors that follow a comment block still match.
    css = raw.replace(/\/\*[\s\S]*?\*\//g, '');
  });

  /**
   * Extracts a rule body whose selector list contains exactly this selector.
   *
   * Walks the stylesheet rather than regex-anchoring on `}`: declaration values
   * contain braces-adjacent characters and functions like `min(...)`/`calc(...)`,
   * which makes brace-relative anchoring unreliable.
   */
  function ruleBody(selector: string): string {
    let i = 0;

    while (i < css.length) {
      const open = css.indexOf('{', i);
      if (open === -1) break;

      const close = css.indexOf('}', open);
      if (close === -1) break;

      const selectors = css
        .slice(i, open)
        .split(',')
        .map((s) => s.trim())
        .filter(Boolean);

      if (selectors.includes(selector)) {
        return css.slice(open + 1, close);
      }

      i = close + 1;
    }

    return '';
  }

  it('html, body and #root fill the viewport', () => {
    const compact = css.replace(/\s+/g, ' ');
    const rule = compact.match(/html\s*,\s*body\s*,\s*#root\s*\{([^}]*)\}/);

    expect(rule).not.toBeNull();
    const body = rule![1];

    expect(body).toMatch(/width:\s*100%/);
    expect(body).toMatch(/height:\s*100%/);
    expect(body).toMatch(/margin:\s*0/);
    expect(body).toMatch(/padding:\s*0/);
  });

  it('the document does not scroll in either axis', () => {
    // Collect every rule that targets html or body.
    const compact = css.replace(/\s+/g, ' ');
    const docRules: string[] = [];

    for (const m of compact.matchAll(/([^{}]*?)\{([^}]*)\}/g)) {
      const selectors = m[1].split(',').map((s) => s.trim());
      if (selectors.includes('html') || selectors.includes('body')) {
        docRules.push(m[2]);
      }
    }

    expect(docRules.length).toBeGreaterThan(0);

    // At least one document-level rule hides overflow...
    expect(docRules.some((r) => /overflow:\s*hidden/.test(r))).toBe(true);

    // ...and no document-level rule enables scrolling in either axis.
    for (const rule of docRules) {
      expect(rule).not.toMatch(/overflow(-[xy])?:\s*(auto|scroll)/);
    }
  });

  it('the game shell fills the viewport without exceeding it', () => {
    const rule = ruleBody('.game-shell');
    expect(rule).not.toBe('');

    expect(rule).toMatch(/position:\s*relative/);
    expect(rule).toMatch(/width:\s*100%/);
    expect(rule).toMatch(/height:\s*100%/);
    expect(rule).toMatch(/overflow:\s*hidden/);
  });

  it('the React overlay is absolutely positioned and cannot expand the document', () => {
    const rule = ruleBody('.game-shell__overlay');
    expect(rule).not.toBe('');

    expect(rule).toMatch(/position:\s*absolute/);
    expect(rule).toMatch(/inset:\s*0/);
  });

  it('does not introduce a web-page content container', () => {
    // No centered/limited-width marketing-style container for the game surface.
    expect(css).not.toMatch(/max-width:\s*1200px/);
    expect(ruleBody('.game-shell')).not.toMatch(/max-width/);
    expect(css).not.toMatch(/margin:\s*(0\s+)?auto/);
  });

  it('never enables auto-scrolling on the game shell', () => {
    expect(ruleBody('.game-shell')).not.toMatch(/overflow(-[xy])?:\s*(auto|scroll)/);
    expect(ruleBody('.game-shell__canvas')).not.toMatch(/overflow(-[xy])?:\s*(auto|scroll)/);
  });

  it('bounds the overlay panel so it cannot exceed the viewport', () => {
    const rule = ruleBody('.status-overlay-card');
    expect(rule).not.toBe('');

    // The panel is clipped by the shell and capped to it.
    expect(rule).toMatch(/max-width:\s*min\(/);
    expect(rule).toMatch(/max-height:\s*calc\(100%\s*-\s*\d+px\)/);
    expect(rule).toMatch(/position:\s*absolute/);
  });

  it('scrolls overlay overflow inside the panel, never the document', () => {
    const panel = ruleBody('.status-overlay-card');
    const body = ruleBody('.status-body');

    // Overflow is intercepted at the inner scroller...
    expect(body).toMatch(/overflow:\s*auto/);
    expect(body).toMatch(/min-height:\s*0/);
    // ...so the panel itself does not expose page scroll.
    expect(panel).toMatch(/overflow:\s*hidden/);
  });
});