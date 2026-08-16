interface ParagraphsProps {
  text: string
  className?: string
}

/** O backend separa parágrafos com linhas em branco (\n\n) — este componente
 * converte isso em elementos <p> reais, em vez de depender de `white-space:
 * pre-wrap` (que preservaria espaçamento irregular vindo do HTML original). */
export function Paragraphs({ text, className = '' }: ParagraphsProps) {
  const paragraphs = text
    .split(/\n{2,}/)
    .map((p) => p.trim())
    .filter(Boolean)

  return (
    <div className={`space-y-3 ${className}`}>
      {paragraphs.map((paragraph, index) => (
        <p key={index}>{paragraph}</p>
      ))}
    </div>
  )
}
