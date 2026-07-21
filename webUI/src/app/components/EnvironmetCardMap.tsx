import { CloudEnvironmentResponse } from "../types";

export function EnvironmetCardMap({ environments }: { environments: CloudEnvironmentResponse[] }) {
    return (
        <section className="ui-panel">
            <h2>Mapa de ambientes</h2>
            <div className="ui-grid">
                {environments.map((environment) => (
                    <article className="ui-card" key={environment.environment}>
                        <div className="ui-section-heading">
                            <h3>{environment.environment}</h3>
                            <span className="ui-operation-status">{environment.observedState}</span>
                        </div>
                        <div className="ui-fact-list">
                            <span>
                                <strong>Project</strong>
                                {environment.projectId}
                            </span>
                            <span>
                                <strong>Region</strong>
                                {environment.region}
                            </span>
                            <span>
                                <strong>Deployable</strong>
                                {String(environment.deployable)}
                            </span>
                            <span>
                                <strong>Evidence</strong>
                                {environment.evidenceLevel}
                            </span>
                        </div>
                        <table className="ui-table">
                            <thead>
                                <tr>
                                    <th>Tipo</th>
                                    <th>Nome</th>
                                    <th>Estado</th>
                                </tr>
                            </thead>
                            <tbody>
                                {environment.resources.map((resource) => (
                                    <tr key={`${environment.environment}-${resource.resourceType}-${resource.name}`}>
                                        <td>{resource.resourceType}</td>
                                        <td>{resource.name}</td>
                                        <td>{resource.state}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </article>
                ))}
            </div>
        </section>
    )
}