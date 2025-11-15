const fs = require('fs');

/**
 * Creates a GitHub issue when configuration drift is detected
 * Usage: node create-drift-issue.js <environment> <github-context-json>
 */

function createDriftIssue(environment, githubContext) {
    const title = `🚨 Configuration Drift Detected in ${environment.toUpperCase()}`;
    
    let driftSummary = 'No drift details available';
    let detailedReport = '';
    
    try {
        const reportJson = fs.readFileSync('drift-report.json', 'utf8');
        const report = JSON.parse(reportJson);
        
        console.log('📊 Successfully parsed drift report');
        console.log('Summary:', report.Summary);
        
        driftSummary = report.Summary;
        
        if (report.ResourceDrifts && report.ResourceDrifts.length > 0) {
            detailedReport = '\n## 📋 Detailed Drift Analysis\n\n';
            
            report.ResourceDrifts.forEach((resource, index) => {
                detailedReport += `### ${index + 1}. ${resource.ResourceType}\n`;
                detailedReport += `**Resource Name:** \`${resource.ResourceName}\`\n\n`;
                
                if (resource.PropertyDrifts && resource.PropertyDrifts.length > 0) {
                    resource.PropertyDrifts.forEach(prop => {
                        const driftTypeEmoji = {
                            0: '❌', // Missing
                            1: '➕', // Extra  
                            2: '🔄', // Modified
                            3: '🆕'  // Added
                        };
                        
                        const driftTypeName = {
                            0: 'Missing Property',
                            1: 'Extra Property', 
                            2: 'Modified Property',
                            3: 'Added Property'
                        };
                        
                        const emoji = driftTypeEmoji[prop.Type] || '🔍';
                        const typeName = driftTypeName[prop.Type] || 'Changed';
                        
                        detailedReport += `**${emoji} ${typeName}:** \`${prop.PropertyPath}\`\n`;
                        detailedReport += `- **Expected:** \`${prop.ExpectedValue}\`\n`;
                        detailedReport += `- **Actual:** \`${prop.ActualValue}\`\n\n`;
                    });
                } else {
                    detailedReport += '- No property drift details available\n\n';
                }
            });
        } else {
            detailedReport = '\n*No detailed drift information available*\n';
        }
        
    } catch (e) {
        console.log('Error parsing drift report:', e.message);
        detailedReport = '\n*Failed to parse drift report details*\n';
        
        // Try to show raw content if JSON parsing failed
        try {
            const rawContent = fs.readFileSync('drift-report.json', 'utf8');
            detailedReport += `\n## Raw Output\n\`\`\`\n${rawContent}\n\`\`\`\n`;
        } catch (readError) {
            detailedReport += '\n*No drift report file available*\n';
        }
    }
    
    const timestamp = new Date().toISOString();
    const issueBody = [
        `🚨 **Configuration drift detected in \`${environment}\` environment!**`,
        '',
        '## 📊 Summary',
        driftSummary,
        detailedReport,
        '',
        `**🕒 Detected at:** ${timestamp}`,
        '**🔧 Action Required:** Review and remediate the configuration drift',
        `**📄 [View Full Workflow Run](${githubContext.serverUrl}/${githubContext.repo.owner}/${githubContext.repo.repo}/actions/runs/${githubContext.runId})**`,
        '',
        '### 💡 Remediation Options',
        '1. **Manual Fix:** Update Azure resources to match the template',
        '2. **Template Update:** Modify the Bicep template if the current state is desired',
        '3. **Auto-fix:** Re-run with `--autofix` flag to automatically deploy template'
    ].join('\n');
    
    return {
        title,
        body: issueBody,
        labels: ['drift-alert', environment, 'needs-review']
    };
}

// Export for use in GitHub Actions
if (typeof module !== 'undefined') {
    module.exports = { createDriftIssue };
}

// CLI usage
if (require.main === module) {
    const environment = process.argv[2];
    const githubContextJson = process.argv[3];
    
    if (!environment || !githubContextJson) {
        console.error('Usage: node create-drift-issue.js <environment> <github-context-json>');
        process.exit(1);
    }
    
    try {
        const githubContext = JSON.parse(githubContextJson);
        const issueData = createDriftIssue(environment, githubContext);
        console.log(JSON.stringify(issueData, null, 2));
    } catch (error) {
        console.error('Error creating drift issue:', error.message);
        process.exit(1);
    }
}