# ai_engine.py
import os, sys, json
from openai import OpenAI

# Hent API-nøgle fra miljøvariabler
client = OpenAI(api_key=os.getenv("OPENAI_API_KEY"))

def generate_reply(email_text):
    """Bruger OpenAI til at generere et kort svar på en mail."""
    prompt = (
        f"Du ern hjælpsom assistent. Skriv et kort, høfligt svar på dansk "
        f"til denne mail:\n\n{email_text}\n\nSvar:"
    )

    try:
        response = client.chat.completions.create(
            model="gpt-4o-mini",
            messages=[{"role": "user", "content": prompt}],
            max_tokens=200,
            temperature=0.3
        )
        return response.choices[0].message.content.strip()
    except Exception as e:
        return f"[Fejl ved OpenAI API: {e}]"
    
if __name__ == "__main__":
    data = sys.stdin.read()
    try:
        payload = json.loads(data or "{}")
        email_text = payload.get("email_text", "")
        result = generate_reply(email_text)
        print(json.dumps({"reply": result}))
    except Exception as e:
        print(json.dumps({"error": str(e)}))