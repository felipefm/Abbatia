import { Card } from '../Card'
import type { OtherSaintResponse } from '../../api/types'

export function OtherSaintsCard({ saints }: { saints: OtherSaintResponse[] }) {
  if (saints.length === 0) return null

  return (
    <Card eyebrow="Também celebrados hoje">
      <ul className="mt-2 space-y-3">
        {saints.map((saint) => (
          <li key={saint.name}>
            <p className="font-serif-reading text-sm font-semibold text-neutral-900 dark:text-neutral-50">
              {saint.name}
            </p>
            <p className="mt-0.5 text-sm text-neutral-600 dark:text-neutral-400">{saint.shortBiography}</p>
          </li>
        ))}
      </ul>
    </Card>
  )
}
