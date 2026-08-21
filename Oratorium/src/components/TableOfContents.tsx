interface TableOfContentsProps {
  hasHomily: boolean
}

const LINK_CLASS = 'text-amber-700 underline decoration-dotted underline-offset-4 dark:text-amber-400'

export function TableOfContents({ hasHomily }: TableOfContentsProps) {
  return (
    <nav
      aria-label="Sumário"
      className="rounded-2xl border border-black/5 bg-white p-4 text-sm shadow-sm dark:border-white/10 dark:bg-neutral-900"
    >
      <ul className="space-y-1.5">
        <li>
          <a href="#saint" className={LINK_CLASS}>
            Santo
          </a>
        </li>
        <li>
          <a href="#readings" className={LINK_CLASS}>
            Leituras
          </a>
        </li>
        {hasHomily && (
          <li>
            <a href="#homily" className={LINK_CLASS}>
              Homilia
            </a>
          </li>
        )}
      </ul>
    </nav>
  )
}
