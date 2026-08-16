import { Link } from 'react-router-dom'

export function AppHeader() {
  return (
    <header className="border-b border-black/5 bg-white/80 backdrop-blur dark:border-white/10 dark:bg-neutral-950/80">
      <div className="mx-auto flex max-w-2xl items-center gap-2 px-4 py-3">
        <Link to="/hoje" className="flex items-center gap-2">
          <img src="/icon-source.svg" alt="" className="h-7 w-7 rounded-md" />
          <span className="font-serif-reading text-lg text-neutral-900 dark:text-neutral-50">Oratorium</span>
        </Link>
      </div>
    </header>
  )
}
