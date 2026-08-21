import type { ReactNode } from 'react'

interface CardProps {
  /** Usado como âncora pelo sumário da barra lateral (ex: id="saint"). */
  id?: string
  eyebrow?: string
  title?: string
  className?: string
  children: ReactNode
}

/** Wrapper compartilhado por todos os cards da página — antes desta
 * extração, cada card (SaintCard, HomilyCard, ReadingsList...) repetia o
 * mesmo className na mão. */
export function Card({ id, eyebrow, title, className = '', children }: CardProps) {
  return (
    <section
      id={id}
      className={`rounded-2xl border border-black/5 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-neutral-900 ${className}`}
    >
      {eyebrow && (
        <p className="text-xs font-semibold tracking-wide text-amber-700 uppercase dark:text-amber-400">{eyebrow}</p>
      )}
      {title && (
        <h2 className="mt-1 font-serif-reading text-xl text-neutral-900 dark:text-neutral-50">{title}</h2>
      )}
      {children}
    </section>
  )
}
