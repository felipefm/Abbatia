import { Card } from '../Card'
import { getMysteryForDate } from '../../lib/rosaryMysteries'

export function RosaryMysteryCard({ date }: { date: string }) {
  const mystery = getMysteryForDate(date)

  return (
    <Card eyebrow="Rosário do dia" title={mystery.name}>
      <ol className="mt-2 space-y-1 text-sm text-neutral-600 dark:text-neutral-400">
        {mystery.items.map((item, index) => (
          <li key={item}>
            {index + 1}. {item}
          </li>
        ))}
      </ol>
    </Card>
  )
}
