"use client";

import { FormEvent, useState } from "react";

type SubmitState =
  | { kind: "idle" }
  | { kind: "submitting" }
  | { kind: "success"; trackingId: string }
  | { kind: "error"; message: string };

const categories = ["Cake", "Shell", "Fountain", "Mine", "Roman candle", "Rocket", "Other"];
const shapes = ["Peony", "Chrysanthemum", "Ring", "Palm", "Mixed / unknown"];

export default function Home() {
  const [state, setState] = useState<SubmitState>({ kind: "idle" });

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setState({ kind: "submitting" });

    const form = event.currentTarget;
    const body = Object.fromEntries(new FormData(form).entries());

    try {
      const response = await fetch("/api/submissions", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(body),
      });
      const result = (await response.json()) as { trackingId?: string; error?: string };
      if (!response.ok || !result.trackingId) {
        throw new Error(result.error ?? "We couldn't save this submission.");
      }

      form.reset();
      setState({ kind: "success", trackingId: result.trackingId });
    } catch (error) {
      setState({
        kind: "error",
        message: error instanceof Error ? error.message : "We couldn't save this submission.",
      });
    }
  }

  return (
    <main>
      <header className="masthead">
        <a className="brand" href="#top" aria-label="Pyro Pilot community catalog home">
          <span className="brand-mark" aria-hidden="true">PP</span>
          <span>PYRO PILOT <b>COMMUNITY CATALOG</b></span>
        </a>
        <a className="quiet-link" href="https://github.com/vornet/pyro-pilot">View the open-source project ↗</a>
      </header>

      <section className="hero" id="top">
        <div className="hero-copy">
          <p className="eyebrow">Built by backyard show designers</p>
          <h1>Know this firework?<br /><em>Share it once.</em></h1>
          <p className="lede">
            Help the next Pyro Pilot user find the product they already own. No GitHub,
            database work, or technical setup required.
          </p>
          <div className="trust-row" aria-label="Submission process">
            <span><b>01</b> Add the label details</span>
            <span><b>02</b> We review it</span>
            <span><b>03</b> Everyone can find it</span>
          </div>
        </div>
        <aside className="safety-note">
          <span className="signal" aria-hidden="true" />
          <div>
            <b>Community reviewed</b>
            <p>Every submission is checked before it reaches the shared catalog.</p>
          </div>
        </aside>
      </section>

      <section className="form-shell" aria-labelledby="submission-title">
        <div className="form-intro">
          <p className="eyebrow">New catalog submission</p>
          <h2 id="submission-title">Tell us what’s on the label.</h2>
          <p>Required fields are marked with an asterisk. Best estimates are welcome—you can leave uncertain effect details blank.</p>
        </div>

        <form onSubmit={submit}>
          <input className="honeypot" name="website" tabIndex={-1} autoComplete="off" aria-hidden="true" />

          <fieldset>
            <legend><span>1</span> Product identity</legend>
            <div className="grid two">
              <label>Manufacturer *<input name="manufacturer" required maxLength={100} placeholder="e.g. Brothers Pyrotechnics" /></label>
              <label>Product name *<input name="productName" required maxLength={140} placeholder="e.g. Hit the Road Jack" /></label>
              <label>Product or item number<input name="productCode" maxLength={80} placeholder="Printed near the barcode" /></label>
              <label>UPC / barcode<input name="upc" inputMode="numeric" maxLength={32} placeholder="Optional" /></label>
            </div>
          </fieldset>

          <fieldset>
            <legend><span>2</span> Performance</legend>
            <div className="grid three">
              <label>Category *<select name="category" required defaultValue=""><option value="" disabled>Choose one</option>{categories.map((value) => <option key={value}>{value}</option>)}</select></label>
              <label>Approx. duration (seconds) *<input name="durationSeconds" required type="number" min="0.1" max="900" step="0.1" placeholder="30" /></label>
              <label>Primary burst shape<select name="burstShape" defaultValue=""><option value="">Not sure</option>{shapes.map((value) => <option key={value}>{value}</option>)}</select></label>
            </div>
            <label>What does it look and sound like?<textarea name="description" maxLength={1200} rows={4} placeholder="Colors, breaks, pace, finale, notable sound…" /></label>
          </fieldset>

          <fieldset>
            <legend><span>3</span> Verification</legend>
            <div className="grid two">
              <label>Product or retailer page *<input name="sourceUrl" required type="url" maxLength={500} placeholder="https://…" /></label>
              <label>Your name or handle<input name="contributorName" maxLength={100} placeholder="How we may credit you" /></label>
              <label className="wide">Email<input name="contributorEmail" type="email" maxLength={254} placeholder="Only used if a reviewer has a question" /><small>Never displayed in the public catalog.</small></label>
            </div>
          </fieldset>

          <label className="consent">
            <input name="attestation" type="checkbox" value="accepted" required />
            <span>I’m sharing factual information I believe is accurate, and my description is my own writing. *</span>
          </label>

          <div className="submit-row">
            <button type="submit" disabled={state.kind === "submitting"}>
              {state.kind === "submitting" ? "Sending…" : "Send for community review"}
            </button>
            <p>Your entry will not be published automatically.</p>
          </div>

          {state.kind === "success" && (
            <div className="notice success" role="status">
              <b>Submission received.</b> Save tracking ID <code>{state.trackingId}</code>.
            </div>
          )}
          {state.kind === "error" && <div className="notice error" role="alert">{state.message}</div>}
        </form>
      </section>

      <footer>
        <span>Pyro Pilot is open source and community maintained.</span>
        <span>Catalog data helps plan shows—it is never firing-safety guidance.</span>
      </footer>
    </main>
  );
}
